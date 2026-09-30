using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.Core.Tests;

public class TrialRulesTests
{
    [Theory]
    [InlineData(12, 9, SuggestedCall.Pass)]
    [InlineData(8, 10, SuggestedCall.Fail)]
    [InlineData(10, 10, SuggestedCall.Tie)]
    public void Strength_compares_totals(int player, int god, SuggestedCall expected)
    {
        SuggestedCall call = TrialRules.Suggest(TrialAspect.Strength, Faces(player), Faces(god));
        Assert.Equal(expected, call);
    }

    [Fact]
    public void Harmony_prefers_the_total_closer_to_11()
    {
        Assert.Equal(SuggestedCall.Pass, TrialRules.Suggest(TrialAspect.Harmony, [4, 3, 4], [6, 6, 2]));
        Assert.Equal(SuggestedCall.Fail, TrialRules.Suggest(TrialAspect.Harmony, [1, 1, 6], [3, 4, 3]));
        Assert.Equal(SuggestedCall.Tie, TrialRules.Suggest(TrialAspect.Harmony, [4, 3, 3], [6, 5, 1]));
    }

    [Fact]
    public void Fear_counts_odd_faces()
    {
        Assert.Equal(SuggestedCall.Pass, TrialRules.Suggest(TrialAspect.Fear, [1, 2, 3], [2, 4, 6]));
        Assert.Equal(SuggestedCall.Fail, TrialRules.Suggest(TrialAspect.Fear, [1, 2, 4], [1, 3, 5]));
        Assert.Equal(SuggestedCall.Tie, TrialRules.Suggest(TrialAspect.Fear, [1, 2, 4], [2, 5, 6]));
    }

    [Fact]
    public void Power_uses_the_same_higher_total_rule()
    {
        Assert.Equal(SuggestedCall.Pass, TrialRules.Suggest(TrialAspect.Power, [6, 6, 6], [8, 1, 1]));
        Assert.Equal(SuggestedCall.Fail, TrialRules.Suggest(TrialAspect.Power, [1, 1, 1], [8, 8, 8]));
    }

    [Fact]
    public void Incomplete_sets_do_not_suggest_a_call()
    {
        Assert.Equal(SuggestedCall.Incomplete, TrialRules.Suggest(TrialAspect.Strength, [6, 6], [1, 1, 1]));
    }

    [Fact]
    public void Zeus_uses_eight_sides_and_waits_between_three_rolls()
    {
        Assert.Equal(8, TrialRules.DieSides(StaffRole.Zeus));
        Assert.Equal(6, TrialRules.DieSides(StaffRole.Ares));
        IReadOnlyList<string> lines = TrialRules.GodRollMacro(StaffRole.Zeus);
        Assert.Equal(["/dice party 8", "/wait 1", "/dice party 8", "/wait 1", "/dice party 8"], lines);
    }

    [Fact]
    public void Zeus_stays_closed_until_the_first_three_trials_are_cleared()
    {
        var mortal = new Participant { Threads = 4 };
        Assert.False(TrialRules.CanRoll(mortal, TrialAspect.Power));
        mortal.Strength = StaffRole.Ares;
        mortal.Harmony = StaffRole.Hera;
        mortal.Fear = StaffRole.Hades;
        Assert.True(TrialRules.CanRoll(mortal, TrialAspect.Power));
        mortal.Threads = 0;
        Assert.False(TrialRules.CanRoll(mortal, TrialAspect.Strength));
    }

    [Fact]
    public void Offering_cost_charges_only_added_threads()
    {
        Assert.Equal(50_000, TrialRules.OfferingCost(4, 6));
        Assert.Equal(0, TrialRules.OfferingCost(6, 4));
        Assert.Equal(0, TrialRules.OfferingCost(10, 10));
        Assert.Equal(10, TrialRules.ClampThreads(14));
        Assert.Equal(0, TrialRules.ClampThreads(-3));
    }

    private static int[] Faces(int total) => [total - 2, 1, 1];
}
