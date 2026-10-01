namespace Pnx.Odyssey.Core;

public sealed class HostArchive
{
    public List<ArchivedSession> Sessions { get; set; } = [];
}

public sealed class ArchivedSession
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public long Revision { get; set; }

    public string LastEditor { get; set; } = "";

    public List<SessionMember> Members { get; set; } = [];

    public List<Participant> Participants { get; set; } = [];

    public List<EditLock> Locks { get; set; } = [];

    public List<TrialBoard> Boards { get; set; } = [];

    public List<LobbyLine> Log { get; set; } = [];

    public List<AspectClaim> Claims { get; set; } = [];

    public List<GodMacro> Macros { get; set; } = [];
}
