using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.Core.Tests;

public class SessionStatsTests
{
    [Fact]
    public void Escape_quotes_csv_cells()
    {
        Assert.Equal("", SessionStats.Escape(null));
        Assert.Equal("plain", SessionStats.Escape("plain"));
        Assert.Equal("\"a,b\"", SessionStats.Escape("a,b"));
        Assert.Equal("\"say \"\"hi\"\"\"", SessionStats.Escape("say \"hi\""));
    }

    [Fact]
    public void Build_counts_roster_claims_and_writes_player_discord()
    {
        SessionSnapshot snapshot = new()
        {
            Id = "night-1",
            Name = "Olympus",
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            Participants =
            [
                new Participant
                {
                    Id = "lulu pillow@phoenix",
                    FirstName = "Lulu",
                    LastName = "Pillow",
                    World = "Phoenix",
                    Discord = "lulu#1234",
                    Threads = 4,
                    Level = 1,
                    Strength = StaffRole.Ares,
                    Harmony = StaffRole.Hera,
                    Fear = StaffRole.Hades,
                },
                new Participant
                {
                    Id = "shade soul@phoenix",
                    FirstName = "Shade",
                    LastName = "Soul",
                    World = "Phoenix",
                    Threads = 0,
                    Level = 1,
                },
            ],
            Claims =
            [
                new AspectClaim
                {
                    Id = "c1",
                    OfferingId = "ash-voice",
                    ParticipantId = "lulu pillow@phoenix",
                    PlayerName = "Lulu Pillow",
                    Run = 1,
                    At = DateTime.UtcNow,
                },
            ],
            Members =
            [
                new SessionMember { Id = "a@phoenix", Name = "Ares Player", World = "Phoenix", Role = StaffRole.Ares, Connected = true },
            ],
            Boards =
            [
                new TrialBoard
                {
                    ParticipantId = "lulu pillow@phoenix",
                    Aspect = TrialAspect.Power,
                    PlayerRolls = [6, 5, 4],
                    GodRolls = [1, 2, 3],
                },
            ],
        };

        SessionStats stats = SessionStats.Build(snapshot, [], [], [], []);
        Assert.Equal(2, stats.Registered);
        Assert.Equal(1, stats.Shades);
        Assert.Equal(1, stats.WithDiscord);
        Assert.Equal(1, stats.Claims);
        Assert.Equal(1, stats.PowerUnlocked);
        Assert.Equal(0, stats.AllCleared);
        Assert.Contains(stats.Highlights, line => line.Contains("Discord", StringComparison.OrdinalIgnoreCase));

        IReadOnlyDictionary<string, string> pack = SessionStats.BuildCsvPack(snapshot, [], [], [], []);
        Assert.Contains("players.csv", pack.Keys);
        Assert.Contains("claims.csv", pack.Keys);
        Assert.Contains("lulu#1234", pack["players.csv"]);
        Assert.Contains("ash-voice", pack["claims.csv"]);
        Assert.Contains("ash_mori", pack["claims.csv"]);
    }
}
