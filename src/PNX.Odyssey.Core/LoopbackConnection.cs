namespace Pnx.Odyssey.Core;

public sealed class LoopbackConnection : IOdysseyConnection
{
    private readonly SessionHost _host = new();
    private readonly Queue<string> _notices = new();
    private CharacterIdentity? _self;
    private DateTime _nextHeartbeat = DateTime.MinValue;

    public LinkState State => _self == null ? LinkState.Offline : LinkState.Connected;

    public string Status => _self == null ? "Waiting for character." : "Local session. No server URL is set.";

    public string? SelfId => _self?.Id;

    public IReadOnlyList<SessionSummary> Sessions { get; private set; } = [];

    public SessionSnapshot? Snapshot { get; private set; }

    public void Connect(CharacterIdentity self, string? pluginKey)
    {
        _self = self;
        Apply(_host.Dispatch(self, new ProtocolMessage
        {
            Type = MessageType.Hello,
            Name = self.Name,
            World = self.World,
            PluginKey = pluginKey,
        }, DateTime.UtcNow));
    }

    public void Disconnect()
    {
        if (_self != null)
            Apply(_host.Dispatch(_self, new ProtocolMessage { Type = MessageType.SessionLeave }, DateTime.UtcNow));

        _self = null;
        Snapshot = null;
    }

    public void Send(ProtocolMessage message)
    {
        if (_self == null)
        {
            _notices.Enqueue("Wait until your character is signed in.");
            return;
        }

        Apply(_host.Dispatch(_self, message, DateTime.UtcNow));
    }

    public void Tick(DateTime utcNow)
    {
        if (_self == null || utcNow < _nextHeartbeat)
            return;

        _nextHeartbeat = utcNow.AddSeconds(10);
        Apply(_host.Dispatch(_self, new ProtocolMessage { Type = MessageType.Heartbeat }, utcNow));
    }

    public string? TakeNotice()
    {
        if (_notices.Count == 0)
            return null;

        return _notices.Dequeue();
    }

    public void Dispose()
    {
    }

    private void Apply(HostResult result)
    {
        Sessions = result.Sessions;
        Snapshot = result.Snapshot;
        if (!result.Ok && !string.IsNullOrWhiteSpace(result.Reason))
            _notices.Enqueue(result.Reason);
    }
}
