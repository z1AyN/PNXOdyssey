using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.Core.Tests;

public class PersistenceTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_restart_keeps_the_table_and_the_close_password()
    {
        var host = new SessionHost();
        var furia = new CharacterIdentity("Furia Bloom", "Phoenix");
        HostResult created = host.Dispatch(furia, new ProtocolMessage
        {
            Type = MessageType.SessionCreate,
            SessionName = "Night",
            Password = "secret",
        }, Now);
        Assert.True(created.Ok);

        var restored = new SessionHost();
        restored.Restore(host.Capture());
        HostResult greeted = restored.Dispatch(furia, new ProtocolMessage
        {
            Type = MessageType.Hello,
            Name = furia.Name,
            World = furia.World,
        }, Now);
        Assert.Equal("Night", greeted.Snapshot?.Name);
        Assert.Equal(1, greeted.Sessions.Single().LiveCount);

        HostResult closed = restored.Dispatch(furia, new ProtocolMessage
        {
            Type = MessageType.SessionClose,
            SessionId = greeted.Snapshot!.Id,
            Password = "secret",
        }, Now);
        Assert.True(closed.Ok);
        Assert.Empty(closed.Sessions);
    }

    [Fact]
    public void Disconnect_drops_the_live_count_without_deleting_the_player()
    {
        var host = new SessionHost();
        var furia = new CharacterIdentity("Furia Bloom", "Phoenix");
        host.Dispatch(furia, new ProtocolMessage
        {
            Type = MessageType.SessionCreate,
            SessionName = "Night",
            Password = "secret",
        }, Now);

        host.Disconnect(furia, Now);
        HostResult viewed = host.Peek(furia, Now);
        Assert.Equal(0, viewed.Sessions.Single().LiveCount);
        Assert.NotNull(viewed.Snapshot);
        Assert.False(viewed.Snapshot!.Members.Single().Connected);
    }
}
