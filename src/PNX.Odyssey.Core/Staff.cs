namespace Pnx.Odyssey.Core;

public enum StaffRole
{
    None = 0,
    Fate,
    Director,
    Ares,
    Athena,
    Hera,
    Aphrodite,
    Hades,
    Poseidon,
    Zeus,
}

public enum TrialAspect
{
    Strength,
    Harmony,
    Fear,
    Power,
}

public static class StaffText
{
    public static string Label(StaffRole role) => role switch
    {
        StaffRole.Fate => "Fate",
        StaffRole.Director => "Director",
        StaffRole.Ares => "Ares",
        StaffRole.Athena => "Athena",
        StaffRole.Hera => "Hera",
        StaffRole.Aphrodite => "Aphrodite",
        StaffRole.Hades => "Hades",
        StaffRole.Poseidon => "Poseidon",
        StaffRole.Zeus => "Zeus",
        _ => "Unassigned",
    };

    public static bool IsGod(StaffRole role) => AspectOf(role) != null;

    public static bool RunsTable(StaffRole role) => role is StaffRole.Fate or StaffRole.Director;

    public static TrialAspect? AspectOf(StaffRole role) => role switch
    {
        StaffRole.Ares or StaffRole.Athena => TrialAspect.Strength,
        StaffRole.Hera or StaffRole.Aphrodite => TrialAspect.Harmony,
        StaffRole.Hades or StaffRole.Poseidon => TrialAspect.Fear,
        StaffRole.Zeus => TrialAspect.Power,
        _ => null,
    };

    public static string AspectName(TrialAspect aspect) => aspect switch
    {
        TrialAspect.Strength => "Trial of Strength",
        TrialAspect.Harmony => "Trial of Harmony",
        TrialAspect.Fear => "Trial of Fear",
        TrialAspect.Power => "Trial of Power",
        _ => aspect.ToString(),
    };

    public static string GodOrDash(StaffRole? role) => role is { } known && known != StaffRole.None
        ? Label(known)
        : "—";
}
