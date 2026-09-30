namespace Pnx.Odyssey.Core;

public readonly record struct PartyPresence(string Name, string World, int Level);

public static class PartyMatcher
{
    public static Participant? SingleChallenger(SessionSnapshot snapshot, IReadOnlyList<PartyPresence> party)
    {
        Participant? found = null;
        foreach (PartyPresence member in party)
        {
            Participant? registered = Find(snapshot, member.Name, member.World);
            if (registered == null || IsStaff(snapshot, registered))
                continue;

            if (found != null)
                return null;

            found = registered;
        }

        return found;
    }

    public static bool IsStaff(SessionSnapshot snapshot, Participant participant) =>
        snapshot.Members.Any(member => member.Connected
            && member.Role != StaffRole.None
            && SamePerson(member.Name, member.World, participant));

    public static Participant? Find(SessionSnapshot snapshot, string name, string world)
    {
        foreach (Participant participant in snapshot.Participants)
        {
            if (SamePerson(name, world, participant))
                return participant;
        }

        return null;
    }

    public static bool SamePerson(string name, string world, Participant participant)
    {
        if (!string.Equals(name.Trim(), participant.FullName, StringComparison.OrdinalIgnoreCase))
            return false;

        return world.Length == 0
            || participant.World.Length == 0
            || string.Equals(world.Trim(), participant.World, StringComparison.OrdinalIgnoreCase);
    }
}

public static class CharacterNames
{
    public static (string First, string Last) Split(string fullName)
    {
        string trimmed = fullName.Trim();
        int space = trimmed.LastIndexOf(' ');
        if (space <= 0)
            return (trimmed, "");

        return (trimmed[..space].Trim(), trimmed[(space + 1)..].Trim());
    }
}

public static class EditScope
{
    public static string Threads(string participantId) => "threads:" + participantId;

    public static string Register(string participantId) => "register:" + participantId;

    public static string Trial(string participantId) => "trial:" + participantId;
}

public static class SessionTime
{
    public static TimeSpan Elapsed(DateTime createdUtc)
    {
        DateTime utc = createdUtc.Kind switch
        {
            DateTimeKind.Local => createdUtc.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(createdUtc, DateTimeKind.Utc),
            _ => createdUtc,
        };
        TimeSpan span = DateTime.UtcNow - utc;
        return span < TimeSpan.Zero ? TimeSpan.Zero : span;
    }

    public static string Format(TimeSpan span)
    {
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h {span.Minutes:00}m";
        if (span.TotalMinutes >= 1)
            return $"{(int)span.TotalMinutes}m {span.Seconds:00}s";
        return $"{Math.Max(0, span.Seconds)}s";
    }
}
