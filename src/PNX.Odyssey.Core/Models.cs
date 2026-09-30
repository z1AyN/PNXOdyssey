namespace Pnx.Odyssey.Core;

public sealed record CharacterIdentity(string Name, string World)
{
    public string Id => $"{Name.Trim()}@{World.Trim()}".ToLowerInvariant();
}

public sealed class Participant
{
    public string Id { get; set; } = "";

    public string FirstName { get; set; } = "";

    public string LastName { get; set; } = "";

    public string World { get; set; } = "";

    public string? Discord { get; set; }

    public int Level { get; set; }

    public int Threads { get; set; }

    public StaffRole? Strength { get; set; }

    public StaffRole? Harmony { get; set; }

    public StaffRole? Fear { get; set; }

    public StaffRole? Power { get; set; }

    public bool Completed { get; set; }

    public long Revision { get; set; }

    public string LastEditor { get; set; } = "";

    public string FullName => $"{FirstName} {LastName}".Trim();

    public bool IsShade => Threads <= 0;

    public Participant Clone() => new()
    {
        Id = Id,
        FirstName = FirstName,
        LastName = LastName,
        World = World,
        Discord = Discord,
        Level = Level,
        Threads = Threads,
        Strength = Strength,
        Harmony = Harmony,
        Fear = Fear,
        Power = Power,
        Completed = Completed,
        Revision = Revision,
        LastEditor = LastEditor,
    };
}

public static class ParticipantIds
{
    public static string Create(string firstName, string lastName, string world) =>
        $"{firstName.Trim()} {lastName.Trim()}@{world.Trim()}".ToLowerInvariant();
}

public sealed class SessionMember
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string World { get; set; } = "";

    public StaffRole Role { get; set; }

    public bool Connected { get; set; }

    public DateTime LastSeen { get; set; }

    public SessionMember Clone() => new()
    {
        Id = Id,
        Name = Name,
        World = World,
        Role = Role,
        Connected = Connected,
        LastSeen = LastSeen,
    };
}

public sealed class SessionSummary
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public int LiveCount { get; set; }
}

public sealed class EditLock
{
    public string Scope { get; set; } = "";

    public string HolderId { get; set; } = "";

    public string HolderName { get; set; } = "";

    public DateTime ExpiresAt { get; set; }

    public EditLock Clone() => new()
    {
        Scope = Scope,
        HolderId = HolderId,
        HolderName = HolderName,
        ExpiresAt = ExpiresAt,
    };
}

public sealed class TrialBoard
{
    public string ParticipantId { get; set; } = "";

    public TrialAspect Aspect { get; set; }

    public List<int> PlayerRolls { get; set; } = [];

    public List<int> GodRolls { get; set; } = [];

    public long Revision { get; set; }

    public string LastEditor { get; set; } = "";

    public TrialBoard Clone() => new()
    {
        ParticipantId = ParticipantId,
        Aspect = Aspect,
        PlayerRolls = [.. PlayerRolls],
        GodRolls = [.. GodRolls],
        Revision = Revision,
        LastEditor = LastEditor,
    };
}

public sealed class SessionSnapshot
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public long Revision { get; set; }

    public List<SessionMember> Members { get; set; } = [];

    public List<Participant> Participants { get; set; } = [];

    public List<EditLock> Locks { get; set; } = [];

    public List<TrialBoard> Boards { get; set; } = [];

    public List<LobbyLine> Log { get; set; } = [];

    public SessionMember? Member(string selfId) =>
        Members.FirstOrDefault(member => string.Equals(member.Id, selfId, StringComparison.OrdinalIgnoreCase));

    public Participant? Participant(string id) =>
        Participants.FirstOrDefault(participant => string.Equals(participant.Id, id, StringComparison.OrdinalIgnoreCase));

    public TrialBoard? Board(string participantId) =>
        Boards.FirstOrDefault(board => string.Equals(board.ParticipantId, participantId, StringComparison.OrdinalIgnoreCase));

    public EditLock? Lock(string scope, DateTime utcNow) =>
        Locks.FirstOrDefault(edit => string.Equals(edit.Scope, scope, StringComparison.Ordinal)
            && edit.ExpiresAt > utcNow);

    public SessionSnapshot Clone() => new()
    {
        Id = Id,
        Name = Name,
        CreatedAt = CreatedAt,
        Revision = Revision,
        Members = Members.Select(member => member.Clone()).ToList(),
        Participants = Participants.Select(participant => participant.Clone()).ToList(),
        Locks = Locks.Select(edit => edit.Clone()).ToList(),
        Boards = Boards.Select(board => board.Clone()).ToList(),
        Log = Log.Select(line => line.Clone()).ToList(),
    };
}
