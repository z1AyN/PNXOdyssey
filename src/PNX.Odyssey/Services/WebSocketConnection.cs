using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Dalamud.Plugin.Services;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.Services;

internal sealed class WebSocketConnection : IOdysseyConnection
{
    private readonly IPluginLog _log;
    private readonly object _gate = new();
    private readonly Queue<string> _notices = new();
    private readonly ConcurrentQueue<ProtocolMessage> _inbound = new();
    private Channel<ProtocolMessage> _outbound = Channel.CreateUnbounded<ProtocolMessage>();
    private CancellationTokenSource? _cts;
    private Task? _run;
    private volatile bool _open;
    private bool _connecting;
    private bool _welcomed;
    private int _pending;
    private string? _error;
    private DateTime _nextAttempt = DateTime.MinValue;
    private volatile bool _pauseRetry;
    private int _generation;
    private CharacterIdentity? _self;
    private string? _pluginKey;

    public WebSocketConnection(IPluginLog log, string url)
    {
        _log = log;
        Url = url.Trim();
    }

    public string Url { get; }

    public LinkState State
    {
        get
        {
            if (!IsValid(Url)) return LinkState.Offline;
            if (_connecting && !_welcomed) return LinkState.Syncing;
            if (!_open || !_welcomed) return LinkState.Offline;
            return _pending > 0 ? LinkState.Syncing : LinkState.Connected;
        }
    }

    public string Status
    {
        get
        {
            if (!IsValid(Url)) return "Server URL must start with ws:// or wss://.";
            if (_connecting && !_welcomed) return "Connecting…";
            if (!_open || !_welcomed) return _error ?? "Offline.";
            return _pending > 0 ? "Syncing." : "Synced with the server.";
        }
    }

    public string? SelfId => _self?.Id;

    public IReadOnlyList<SessionSummary> Sessions { get; private set; } = [];

    public SessionSnapshot? Snapshot { get; private set; }

    public static string? CanonicalUrl(string url)
    {
        string trimmed = url.Trim();
        if (trimmed.Length == 0) return null;
        if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            trimmed = "wss://" + trimmed["https://".Length..];
        else if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            trimmed = "ws://" + trimmed["http://".Length..];
        return trimmed;
    }

    public static bool IsValid(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == "ws" || uri.Scheme == "wss");

    public void Connect(CharacterIdentity self, string? pluginKey)
    {
        _self = self;
        _pluginKey = pluginKey;
        _pauseRetry = false;
        _error = null;
        Start();
    }

    public void Disconnect()
    {
        Stop();
        Snapshot = null;
    }

    public void Send(ProtocolMessage message)
    {
        if (Tracks(message.Type))
            Interlocked.Increment(ref _pending);

        if (!_outbound.Writer.TryWrite(message) && Tracks(message.Type))
            Interlocked.Decrement(ref _pending);
    }

    public void Tick(DateTime utcNow)
    {
        while (_inbound.TryDequeue(out ProtocolMessage? message))
            Apply(message);

        if (_self != null && !_pauseRetry && !_open && !_connecting && utcNow >= _nextAttempt && IsValid(Url))
            Start();
    }

    public string? TakeNotice()
    {
        lock (_gate)
        {
            if (_notices.Count == 0) return null;
            return _notices.Dequeue();
        }
    }

    public void Dispose() => Stop();

    private void Start()
    {
        if (_self == null || !IsValid(Url))
        {
            _error = IsValid(Url) ? null : "Server URL must start with ws:// or wss://.";
            return;
        }

        Stop();
        int generation = Interlocked.Increment(ref _generation);
        _outbound = Channel.CreateUnbounded<ProtocolMessage>();
        _cts = new CancellationTokenSource();
        _connecting = true;
        _error = null;
        var hello = new ProtocolMessage
        {
            Type = MessageType.Hello,
            Name = _self.Name,
            World = _self.World,
            PluginKey = string.IsNullOrWhiteSpace(_pluginKey) ? null : _pluginKey,
        };
        _run = Task.Run(() => RunAsync(hello, _cts.Token, generation));
    }

    private void Stop()
    {
        _connecting = false;
        _open = false;
        _welcomed = false;
        _pending = 0;
        try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
        _outbound.Writer.TryComplete();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunAsync(ProtocolMessage hello, CancellationToken cancel, int generation)
    {
        using var socket = new ClientWebSocket();
        try
        {
            await socket.ConnectAsync(new Uri(Url), cancel).ConfigureAwait(false);
            _open = true;
            await SendRawAsync(socket, hello, cancel).ConfigureAwait(false);
            Task receive = ReceiveLoopAsync(socket, cancel);
            Task send = SendLoopAsync(socket, cancel);
            await Task.WhenAll(receive, send).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _error = "The server connection failed.";
            _log.Warning(ex, "Odyssey server connection failed.");
        }
        finally
        {
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.Debug(ex, "Odyssey socket close was ignored.");
                }
            }

            if (generation == Volatile.Read(ref _generation))
            {
                _open = false;
                _connecting = false;
                _welcomed = false;
                _pending = 0;
                _nextAttempt = DateTime.UtcNow.AddSeconds(5);
            }
        }
    }

    private async Task SendLoopAsync(ClientWebSocket socket, CancellationToken cancel)
    {
        await foreach (ProtocolMessage message in _outbound.Reader.ReadAllAsync(cancel).ConfigureAwait(false))
            await SendRawAsync(socket, message, cancel).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancel)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();
        while (socket.State == WebSocketState.Open && !cancel.IsCancellationRequested)
        {
            stream.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, cancel).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                    return;
                stream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            if (result.MessageType != WebSocketMessageType.Text)
                continue;

            ProtocolMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<ProtocolMessage>(stream.GetBuffer().AsSpan(0, (int)stream.Length), ProtocolJson.Options);
            }
            catch (JsonException ex)
            {
                _log.Warning(ex, "Ignored a malformed Odyssey message.");
                continue;
            }

            if (message != null)
                _inbound.Enqueue(message);
        }
    }

    private static async Task SendRawAsync(ClientWebSocket socket, ProtocolMessage message, CancellationToken cancel)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(message, ProtocolJson.Options);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancel).ConfigureAwait(false);
    }

    private void Apply(ProtocolMessage message)
    {
        switch (message.Type)
        {
            case MessageType.Welcome:
                _welcomed = true;
                _pending = 0;
                if (message.Sessions != null) Sessions = message.Sessions;
                Snapshot = message.Session;
                break;
            case MessageType.Sessions:
                if (message.Sessions != null) Sessions = message.Sessions;
                break;
            case MessageType.State:
                if (message.Sessions != null) Sessions = message.Sessions;
                Snapshot = message.Session;
                if (_pending > 0) Interlocked.Decrement(ref _pending);
                break;
            case MessageType.Rejected:
                if (message.Sessions != null) Sessions = message.Sessions;
                Snapshot = message.Session;
                if (_pending > 0) Interlocked.Decrement(ref _pending);
                if (!string.IsNullOrWhiteSpace(message.Reason))
                {
                    _error = message.Reason;
                    lock (_gate)
                        _notices.Enqueue(message.Reason);
                }

                if (!_welcomed)
                    _pauseRetry = true;
                break;
        }
    }

    private static bool Tracks(string type) => type is
        MessageType.SessionCreate
        or MessageType.SessionJoin
        or MessageType.SessionClose
        or MessageType.SessionLeave
        or MessageType.RoleSet
        or MessageType.ParticipantRegister
        or MessageType.ParticipantRemove
        or MessageType.ParticipantLevel
        or MessageType.ParticipantComplete
        or MessageType.ParticipantEdit
        or MessageType.ThreadsSet
        or MessageType.TrialDice
        or MessageType.TrialFail
        or MessageType.TrialPass
        or MessageType.LobbyChat
        or MessageType.AspectClaim
        or MessageType.AspectRemove
        or MessageType.GodMacroSave
        or MessageType.GodMacroRemove;
}
