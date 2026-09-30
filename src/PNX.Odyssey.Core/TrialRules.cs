namespace Pnx.Odyssey.Core;

public enum SuggestedCall
{
    Incomplete,
    Pass,
    Fail,
    Tie,
}

public static class TrialRules
{
    public const int StartingThreads = 4;
    public const int MaxThreads = 10;
    public const int MinStartingThreads = 1;
    public const int GilPerThread = 25_000;
    public const int DicePerSet = 3;
    public const int MortalSides = 6;
    public const int PairGodSides = 6;
    public const int ZeusSides = 8;
    public const int HarmonyTarget = 11;

    public static int DieSides(StaffRole role) => role == StaffRole.Zeus ? ZeusSides : PairGodSides;

    public static IReadOnlyList<string> GodRollMacro(StaffRole role)
    {
        string line = $"/dice party {DieSides(role)}";
        return [line, "/wait 1", line, "/wait 1", line];
    }

    public static int OfferingCost(int current, int next)
    {
        int delta = next - current;
        return delta > 0 ? delta * GilPerThread : 0;
    }

    public static int ClampThreads(int threads) => Math.Clamp(threads, 0, MaxThreads);

    public static bool PowerUnlocked(Participant participant) =>
        participant.Strength != null && participant.Harmony != null && participant.Fear != null;

    public static bool CanRoll(Participant participant, TrialAspect aspect)
    {
        if (participant.Threads < 1) return false;
        return aspect != TrialAspect.Power || PowerUnlocked(participant);
    }

    public static StaffRole? Victor(Participant participant, TrialAspect aspect) => aspect switch
    {
        TrialAspect.Strength => participant.Strength,
        TrialAspect.Harmony => participant.Harmony,
        TrialAspect.Fear => participant.Fear,
        TrialAspect.Power => participant.Power,
        _ => null,
    };

    public static bool AllTrialsCleared(Participant participant) =>
        participant.Strength != null
        && participant.Harmony != null
        && participant.Fear != null
        && participant.Power != null;

    public static SuggestedCall Suggest(TrialAspect aspect, IReadOnlyList<int> playerRolls, IReadOnlyList<int> godRolls)
    {
        RollSet player = DiceGrouping.Latest(playerRolls);
        RollSet god = DiceGrouping.Latest(godRolls);
        if (!player.IsComplete || !god.IsComplete)
            return SuggestedCall.Incomplete;

        return aspect switch
        {
            TrialAspect.Harmony => CompareLower(Distance(player.Total), Distance(god.Total)),
            TrialAspect.Fear => CompareHigher(player.OddCount, god.OddCount),
            _ => CompareHigher(player.Total, god.Total),
        };
    }

    public static bool RollsReady(IReadOnlyList<int> playerRolls, IReadOnlyList<int> godRolls) =>
        Suggest(TrialAspect.Strength, playerRolls, godRolls) != SuggestedCall.Incomplete;

    private static int Distance(int total) => Math.Abs(total - HarmonyTarget);

    private static SuggestedCall CompareHigher(int player, int god) =>
        player > god ? SuggestedCall.Pass : player < god ? SuggestedCall.Fail : SuggestedCall.Tie;

    private static SuggestedCall CompareLower(int playerDistance, int godDistance) =>
        playerDistance < godDistance ? SuggestedCall.Pass
        : playerDistance > godDistance ? SuggestedCall.Fail
        : SuggestedCall.Tie;
}
