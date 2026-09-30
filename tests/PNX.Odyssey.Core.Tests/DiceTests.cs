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
    [InlineData("rolls a 4 (out of 6)!", 4)]
    [InlineData("1 5", 5)]
    [InlineData("11", 11)]
    [InlineData("no digits", null)]
    public void Chat_text_uses_the_first_number(string text, int? expected)
    {
        Assert.Equal(expected, DiceText.ReadRoll(text));
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
