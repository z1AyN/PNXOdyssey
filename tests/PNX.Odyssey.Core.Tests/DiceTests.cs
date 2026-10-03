using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.Core.Tests;

public class DiceTests
{
    [Fact]
    public void Rolls_group_into_sets_of_three()
    {
        Assert.Empty(DiceGrouping.Group([]));

        IReadOnlyList<RollSet> six = DiceGrouping.Group([1, 2, 3, 4, 5, 6]);
        Assert.Equal(2, six.Count);
        Assert.True(six[0].IsComplete);
        Assert.Equal(6, six[0].Total);
        Assert.Equal(2, six[0].OddCount);

        IReadOnlyList<RollSet> four = DiceGrouping.Group([6, 6, 6, 2]);
        Assert.Equal(2, four.Count);
        Assert.False(four[1].IsComplete);
        Assert.Equal([2], four[1].Faces);
    }

    [Theory]
    [InlineData("Random! (1-6) 2", 2)]
    [InlineData("rolls a 4 (out of 6)!", 4)]
    [InlineData("1 5", 5)]
    [InlineData("11", 11)]
    [InlineData("no digits", null)]
    public void Chat_text_uses_the_first_number(string text, int? expected)
    {
        Assert.Equal(expected, DiceText.ReadRoll(text));
    }

    [Theory]
    [InlineData("Random! (1-6) 2", 2, 6)]
    [InlineData("Random! (1-8) 7", 7, 8)]
    [InlineData("random! (1–6) 4", 4, 6)]
    [InlineData("rolls a 4 (out of 6)!", 4, 6)]
    [InlineData("rolls a 4 (out of 100)!", 4, 100)]
    [InlineData("rolls a 7 (out of 8)!", 7, 8)]
    public void Chat_text_reads_the_die_size(string text, int roll, int sides)
    {
        Assert.True(DiceText.TryRead(text, out int readRoll, out int readSides));
        Assert.Equal(roll, readRoll);
        Assert.Equal(sides, readSides);
    }

    [Theory]
    [InlineData("rolls a 4!")]
    [InlineData("5")]
    [InlineData("need 3 threads")]
    [InlineData("1 5")]
    [InlineData("(1-6) 2")]
    [InlineData("Random! please help")]
    public void Bare_numbers_are_not_dice_rolls(string text)
    {
        Assert.False(DiceText.TryRead(text, out _, out _));
    }

    [Fact]
    public void Remaining_claims_sum_a_person_and_count_a_shared_pool_once()
    {
        IReadOnlyList<string> open = AspectCatalog.RemainingSummary([]);
        Assert.Contains("Ash Yusira (2)", open);
        Assert.Contains("Chocola Strawberry (2)", open);
        Assert.Contains("Cassian Hyskaris (1)", open);
        Assert.DoesNotContain(open, line => line.StartsWith("Ash Yusira", StringComparison.Ordinal) && line != "Ash Yusira (2)");

        var used = new List<AspectClaim> { new() { OfferingId = "ash-voice" }, new() { OfferingId = "cassian-rp" } };
        IReadOnlyList<string> left = AspectCatalog.RemainingSummary(used);
        Assert.Contains("Ash Yusira (1)", left);
        Assert.DoesNotContain("Cassian Hyskaris (1)", left);
        Assert.DoesNotContain("Cassian Hyskaris (0)", left);
    }
}

public class DjScheduleTests
{
    [Fact]
    public void The_current_dj_is_the_latest_slot_that_has_started()
    {
        long[] starts = [100, 200, 300];
        DjSchedule.Pick before = DjSchedule.Choose(starts, 50);
        Assert.Equal(-1, before.Current);
        Assert.Equal(0, before.Next);

        DjSchedule.Pick mid = DjSchedule.Choose(starts, 200);
        Assert.Equal(1, mid.Current);
        Assert.Equal(2, mid.Next);

        DjSchedule.Pick after = DjSchedule.Choose(starts, 400);
        Assert.Equal(2, after.Current);
        Assert.Equal(-1, after.Next);
        Assert.Equal("Now playing: Kiwi (twitch.tv/kiwi). Next: Khangomon.", DjSchedule.Shout("Kiwi", "twitch.tv/kiwi", "Khangomon"));
    }
}

public class PartyMatcherTests
{
    [Fact]
    public void One_mortal_in_the_party_is_the_challenger()
    {
        SessionSnapshot snapshot = Sample();
        PartyPresence[] party =
        [
            new("Ares Player", "Phoenix", 90),
            new("Lulu Pillow", "Phoenix", 100),
        ];

        Participant? challenger = PartyMatcher.SingleChallenger(snapshot, party);
        Assert.Equal("lulu pillow@phoenix", challenger?.Id);
    }

    [Fact]
    public void Two_mortals_are_not_auto_selected()
    {
        SessionSnapshot snapshot = Sample();
        snapshot.Participants.Add(new Participant
        {
            Id = "ramune soda@phoenix",
            FirstName = "Ramune",
            LastName = "Soda",
            World = "Phoenix",
        });

        PartyPresence[] party =
        [
            new("Lulu Pillow", "Phoenix", 100),
            new("Ramune Soda", "Phoenix", 90),
        ];

        Assert.Null(PartyMatcher.SingleChallenger(snapshot, party));
    }

    private static SessionSnapshot Sample() => new()
    {
        Members =
        [
            new SessionMember { Name = "Ares Player", World = "Phoenix", Role = StaffRole.Ares, Connected = true },
        ],
        Participants =
        [
            new Participant
            {
                Id = "lulu pillow@phoenix",
                FirstName = "Lulu",
                LastName = "Pillow",
                World = "Phoenix",
            },
            new Participant
            {
                Id = "ares player@phoenix",
                FirstName = "Ares",
                LastName = "Player",
                World = "Phoenix",
            },
        ],
    };
}
