using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.Server;

public sealed class OdysseyGateway
{
    private const int MaxFrameBytes = 256 * 1024;
    private readonly SessionHost _host = new();
    private readonly StateStore _store;
    private readonly byte[] _pluginKeyHash;
    private readonly object _gate = new();
    private readonly List<ClientSocket> _clients = [];
    private readonly ILogger<OdysseyGateway> _log;

    public OdysseyGateway(StateStore store, string pluginKey, ILogger<OdysseyGateway> log)
    {
        _store = store;
        _pluginKeyHash = Hash(pluginKey);
        _log = log;
        _host.Restore(store.Load());
    }

    public async Task Run(WebSocket socket, CancellationToken cancel)
    {
        var client = new ClientSocket(socket);
        lock (_gate)
            _clients.Add(client);

        try
        {
            await ReadLoop(client, cancel);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
        }
        catch (WebSocketException ex)
        {
            _log.LogDebug(ex, "Odyssey socket closed.");
        }
        finally
        {
            List<Outbound> notices = [];
            lock (_gate)
            {
                _clients.Remove(client);
                if (client.Identity != null)
                {
                    DateTime now = DateTime.UtcNow;
                    _host.Disconnect(client.Identity, now);
                    notices = Views(now);
                    _store.Save(_host.Capture());
                }
            }

            await SendAll(notices);
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                }
                catch (WebSocketException)
                {
                }
            }
        }
    }

    private async Task ReadLoop(ClientSocket client, CancellationToken cancel)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();
        while (client.Socket.State == WebSocketState.Open && !cancel.IsCancellationRequested)
        {
            stream.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                result = await client.Socket.ReceiveAsync(buffer, cancel);
                if (result.MessageType == WebSocketMessageType.Close)
                    return;
                stream.Write(buffer, 0, result.Count);
                if (stream.Length > MaxFrameBytes)
                    return;
            }
            while (!result.EndOfMessage);

            if (result.MessageType != WebSocketMessageType.Text)
                continue;

            ProtocolMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<ProtocolMessage>(stream.GetBuffer().AsSpan(0, (int)stream.Length), ProtocolJson.Options);
            }
            catch (JsonException)
            {
                await Send(client.Socket, new ProtocolMessage { Type = MessageType.Rejected, Reason = "That message was not valid JSON." });
                continue;
            }

            if (message == null || string.IsNullOrWhiteSpace(message.Type))
                continue;

            if (!client.Greeted)
            {
                if (message.Type != MessageType.Hello || !await Greet(client, message))
                    return;
                continue;
            }

            await Dispatch(client, message);
        }
    }

    private async Task<bool> Greet(ClientSocket client, ProtocolMessage message)
    {
        if (!KeyMatches(message.PluginKey))
        {
            await Send(client.Socket, new ProtocolMessage { Type = MessageType.Rejected, Reason = "Plugin key was rejected." });
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.Name) || string.IsNullOrWhiteSpace(message.World))
        {
            await Send(client.Socket, new ProtocolMessage { Type = MessageType.Rejected, Reason = "A character name and world are required." });
            return false;
        }

        var identity = new CharacterIdentity(message.Name.Trim(), message.World.Trim());
        List<Outbound> notices;
        ProtocolMessage welcome;
        lock (_gate)
        {
            client.Identity = identity;
            client.Greeted = true;
            HostResult result = _host.Dispatch(identity, new ProtocolMessage { Type = MessageType.Hello }, DateTime.UtcNow);
            welcome = Envelope(MessageType.Welcome, result, identity.Id);
            notices = Views(DateTime.UtcNow).Where(item => item.Client != client).ToList();
        }

        await Send(client.Socket, welcome);
        await SendAll(notices);
        _log.LogInformation("Odyssey hello from {Name}.", identity.Id);
        return true;
    }

    private async Task Dispatch(ClientSocket client, ProtocolMessage message)
    {
        if (client.Identity == null)
            return;

        List<Outbound> notices;
        ProtocolMessage reply;
        bool save = message.Type != MessageType.Heartbeat;
        lock (_gate)
        {
            DateTime now = DateTime.UtcNow;
            HostResult result = _host.Dispatch(client.Identity, message, now);
            reply = Envelope(result.Ok ? MessageType.State : MessageType.Rejected, result, null);
            notices = Views(now).Where(item => item.Client != client).ToList();
            if (save)
                _store.Save(_host.Capture());
        }

        if (!resultQuiet(message.Type))
            _log.LogInformation("Odyssey {Type} from {Name} accepted {Ok}.", message.Type, client.Identity.Id, reply.Type != MessageType.Rejected);

        await Send(client.Socket, reply);
        await SendAll(notices);
    }

    private List<Outbound> Views(DateTime utcNow)
    {
        var notices = new List<Outbound>();
        foreach (ClientSocket client in _clients)
        {
            if (client.Identity == null)
                continue;
            HostResult view = _host.Peek(client.Identity, utcNow);
            notices.Add(new Outbound(client, Envelope(MessageType.State, view, null)));
        }

        return notices;
    }

    private static ProtocolMessage Envelope(string type, HostResult result, string? selfId) => new()
    {
        Type = type,
        SelfId = selfId,
        Reason = result.Reason,
        SavedBy = result.SavedBy,
        Sessions = result.Sessions.ToList(),
        Session = result.Snapshot,
    };

    private bool KeyMatches(string? provided)
    {
        byte[] actual = Hash(provided ?? "");
        return CryptographicOperations.FixedTimeEquals(actual, _pluginKeyHash);
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    private static bool resultQuiet(string type) => type is MessageType.Heartbeat or MessageType.EditBegin or MessageType.EditEnd;

    private static async Task SendAll(IReadOnlyList<Outbound> messages)
    {
        foreach (Outbound message in messages)
            await Send(message.Client.Socket, message.Body);
    }

    private static async Task Send(WebSocket socket, ProtocolMessage message)
    {
        if (socket.State != WebSocketState.Open)
            return;

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(message, ProtocolJson.Options);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private sealed class ClientSocket(WebSocket socket)
    {
        public WebSocket Socket { get; } = socket;

        public CharacterIdentity? Identity { get; set; }

        public bool Greeted { get; set; }
    }

    private readonly record struct Outbound(ClientSocket Client, ProtocolMessage Body);
}
