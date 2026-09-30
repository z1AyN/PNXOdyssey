using System.Text.Json;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.Core.Tests;

public class SessionHostTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_lists_a_live_session_and_close_requires_the_password()
    {
        var host = new SessionHost();
        CharacterIdentity furia = Person("Furia Bloom");
        HostResult created = host.Dispatch(furia, new ProtocolMessage
        {
            Type = MessageType.SessionCreate,
            SessionName = "Night",
            Password = "secret",
        }, Now);

        Assert.True(created.Ok);
        Assert.Equal("Night", created.Snapshot!.Name);
        Assert.Equal(1, created.Sessions.Single().LiveCount);
        string sessionId = created.Snapshot.Id;

        HostResult wrong = host.Dispatch(furia, new ProtocolMessage
        {
            Type = MessageType.SessionClose,
            SessionId = sessionId,
            Password = "nope",
        }, Now);
        Assert.False(wrong.Ok);
        Assert.Single(wrong.Sessions);

        HostResult closed = host.Dispatch(furia, new ProtocolMessage
        {
            Type = MessageType.SessionClose,
            SessionId = sessionId,
            Password = "secret",
        }, Now);
        Assert.True(closed.Ok);
        Assert.Empty(closed.Sessions);
        Assert.Null(closed.Snapshot);
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(created.Snapshot, ProtocolJson.Options), StringComparison.Ordinal);
    }

    [Fact]
    public void God_roles_are_unique_and_fate_is_not()
    {
        (SessionHost host, string sessionId) = Open();
        CharacterIdentity furia = Person("Furia Bloom");
        CharacterIdentity ramune = Person("Ramune Soda");
        Join(host, ramune, sessionId);

        Assert.True(SetRole(host, furia, StaffRole.Fate).Ok);
        Assert.True(SetRole(host, ramune, StaffRole.Fate).Ok);

        Assert.True(SetRole(host, furia, StaffRole.Ares).Ok);
        HostResult taken = SetRole(host, ramune, StaffRole.Ares);
        Assert.False(taken.Ok);
        Assert.Contains("Furia Bloom", taken.Reason, StringComparison.Ordinal);

        Assert.True(SetRole(host, ramune, StaffRole.Athena).Ok);
    }

    [Fact]
    public void Disconnected_players_drop_out_of_the_live_count()
    {
        (SessionHost host, string sessionId) = Open();
        Join(host, Person("Ramune Soda"), sessionId);

        HostResult later = host.Dispatch(Person("Furia Bloom"), new ProtocolMessage { Type = MessageType.Heartbeat }, Now.AddSeconds(30));
        Assert.Equal(1, later.Sessions.Single().LiveCount);
    }

    [Fact]
    public void Stale_thread_edits_name_who_saved_first()
    {
        (SessionHost host, string sessionId, Participant mortal) = SeatedMortal();
        CharacterIdentity ramune = Person("Ramune Soda");
        Join(host, ramune, sessionId);
        SetRole(host, ramune, StaffRole.Fate);

        HostResult saved = SetThreads(host, Person("Furia Bloom"), mortal.Id, 6, mortal.Revision);
        Assert.True(saved.Ok);
        Assert.Equal(6, saved.Snapshot!.Participant(mortal.Id)!.Threads);

        HostResult stale = SetThreads(host, ramune, mortal.Id, 7, mortal.Revision);
        Assert.False(stale.Ok);
        Assert.Equal("Furia Bloom saved first.", stale.Reason);
        Assert.Equal("Furia Bloom", stale.SavedBy);
        Assert.Equal(6, stale.Snapshot!.Participant(mortal.Id)!.Threads);
    }

    [Fact]
    public void Only_a_fate_can_change_threads_inside_the_cap()
    {
        (SessionHost host, _, Participant mortal) = SeatedMortal();
        SetRole(host, Person("Furia Bloom"), StaffRole.Ares);
        HostResult denied = SetThreads(host, Person("Furia Bloom"), mortal.Id, 5, mortal.Revision);
        Assert.False(denied.Ok);

        SetRole(host, Person("Furia Bloom"), StaffRole.Fate);
        Assert.False(SetThreads(host, Person("Furia Bloom"), mortal.Id, 11, mortal.Revision).Ok);
        Assert.True(SetThreads(host, Person("Furia Bloom"), mortal.Id, 0, mortal.Revision).Ok);
    }

    [Fact]
    public void Pass_records_the_first_god_and_a_second_pass_keeps_them()
    {
        (SessionHost host, Participant mortal) = AresTrial();
        HostResult passed = Pass(host, Person("Furia Bloom"), mortal.Id, 1);
        Assert.True(passed.Ok);
        Assert.Equal(StaffRole.Ares, passed.Snapshot!.Participant(mortal.Id)!.Strength);
        Assert.Equal(4, passed.Snapshot.Participant(mortal.Id)!.Threads);
        Assert.Empty(passed.Snapshot.Board(mortal.Id)!.PlayerRolls);

        HostResult again = Pass(host, Person("Furia Bloom"), mortal.Id, passed.Snapshot.Participant(mortal.Id)!.Revision);
        Assert.True(again.Ok);
        Assert.Equal(StaffRole.Ares, again.Snapshot!.Participant(mortal.Id)!.Strength);
    }

    [Fact]
    public void Fail_spends_a_thread_and_keeps_the_trial_open()
    {
        (SessionHost host, Participant mortal) = AresTrial();
        HostResult failed = host.Dispatch(Person("Furia Bloom"), new ProtocolMessage
        {
            Type = MessageType.TrialFail,
            ParticipantId = mortal.Id,
            Revision = mortal.Revision,
        }, Now);

        Assert.True(failed.Ok);
        Participant updated = failed.Snapshot!.Participant(mortal.Id)!;
        Assert.Equal(3, updated.Threads);
        Assert.Null(updated.Strength);
        Assert.Empty(failed.Snapshot.Board(mortal.Id)!.PlayerRolls);
    }

    [Fact]
    public void Zeus_and_shades_cannot_pass()
    {
        (SessionHost host, string _, Participant mortal) = SeatedMortal();
        CharacterIdentity zeus = Person("Ink Sen");
        Join(host, zeus, host.Dispatch(Person("Furia Bloom"), new ProtocolMessage { Type = MessageType.Heartbeat }, Now).Snapshot!.Id);
        SetRole(host, zeus, StaffRole.Zeus);
        RollBoth(host, zeus, mortal.Id, TrialAspect.Power);

        HostResult blocked = Pass(host, zeus, mortal.Id, mortal.Revision);
        Assert.False(blocked.Ok);
        Assert.Contains("Zeus", blocked.Reason, StringComparison.Ordinal);
        host.Dispatch(zeus, new ProtocolMessage
        {
            Type = MessageType.EditEnd,
            Scope = EditScope.Trial(mortal.Id),
        }, Now);

        SetRole(host, Person("Furia Bloom"), StaffRole.Fate);
        SetThreads(host, Person("Furia Bloom"), mortal.Id, 0, mortal.Revision);
        SetRole(host, Person("Furia Bloom"), StaffRole.Ares);
        long revision = host.Dispatch(Person("Furia Bloom"), new ProtocolMessage { Type = MessageType.Heartbeat }, Now)
            .Snapshot!.Participant(mortal.Id)!.Revision;
        RollBoth(host, Person("Furia Bloom"), mortal.Id, TrialAspect.Strength);
        HostResult shade = Pass(host, Person("Furia Bloom"), mortal.Id, revision);
        Assert.False(shade.Ok);
        Assert.Contains("shade", shade.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dice_append_when_the_revision_is_behind_and_reject_a_conflicting_replace()
    {
        (SessionHost host, string _, Participant mortal) = SeatedMortal();
        CharacterIdentity ares = Person("Furia Bloom");
        SetRole(host, ares, StaffRole.Ares);

        HostResult first = Dice(host, ares, mortal.Id, "player", [4], 0);
        Assert.True(first.Ok);
        Assert.Equal(1, first.Snapshot!.Board(mortal.Id)!.Revision);

        HostResult append = Dice(host, ares, mortal.Id, "player", [4, 5], 0);
        Assert.True(append.Ok);
        Assert.Equal([4, 5], append.Snapshot!.Board(mortal.Id)!.PlayerRolls);

        HostResult replace = Dice(host, ares, mortal.Id, "player", [2], 0);
        Assert.False(replace.Ok);
        Assert.Equal("Furia Bloom saved first.", replace.Reason);
        Assert.Equal([4, 5], replace.Snapshot!.Board(mortal.Id)!.PlayerRolls);

        Assert.False(Dice(host, ares, mortal.Id, "player", [4, 5, 7], append.Snapshot.Board(mortal.Id)!.Revision).Ok);

        long revision = append.Snapshot.Board(mortal.Id)!.Revision;
        HostResult cleared = Dice(host, ares, mortal.Id, "player", [], revision);
        Assert.True(cleared.Ok);
        Assert.Empty(cleared.Snapshot!.Board(mortal.Id)!.PlayerRolls);
    }

    [Fact]
    public void An_open_edit_is_visible_until_it_expires()
    {
        (SessionHost host, string sessionId) = Open();
        CharacterIdentity ramune = Person("Ramune Soda");
        Join(host, ramune, sessionId);
        SetRole(host, Person("Furia Bloom"), StaffRole.Fate);
        SetRole(host, ramune, StaffRole.Fate);
        const string scope = "threads:lulu";

        host.Dispatch(Person("Furia Bloom"), new ProtocolMessage { Type = MessageType.EditBegin, Scope = scope }, Now);
        HostResult seen = host.Dispatch(ramune, new ProtocolMessage { Type = MessageType.EditBegin, Scope = scope }, Now.AddSeconds(1));
        EditLock? held = seen.Snapshot!.Lock(scope, Now.AddSeconds(1));
        Assert.Equal(Person("Furia Bloom").Id, held?.HolderId);

        HostResult expired = host.Dispatch(ramune, new ProtocolMessage { Type = MessageType.Heartbeat }, Now.AddSeconds(21));
        Assert.Null(expired.Snapshot!.Lock(scope, Now.AddSeconds(21)));
    }

    [Fact]
    public void Complete_requires_every_trial_and_a_fate()
    {
        (SessionHost host, Participant mortal) = AresTrial();
        CharacterIdentity fate = Person("Masha Shiri");
        string sessionId = host.Dispatch(Person("Furia Bloom"), new ProtocolMessage { Type = MessageType.Heartbeat }, Now).Snapshot!.Id;
        Join(host, fate, sessionId);
        SetRole(host, fate, StaffRole.Fate);

        HostResult early = Complete(host, fate, mortal.Id, mortal.Revision);
        Assert.False(early.Ok);

        long revision = Clear(host, Person("Furia Bloom"), mortal.Id, mortal.Revision);
        CharacterIdentity hera = Person("Hela Light");
        Join(host, hera, sessionId);
        SetRole(host, hera, StaffRole.Hera);
        revision = Clear(host, hera, mortal.Id, revision);
        CharacterIdentity hades = Person("Xeron Ak");
        Join(host, hades, sessionId);
        SetRole(host, hades, StaffRole.Hades);
        revision = Clear(host, hades, mortal.Id, revision);
        CharacterIdentity zeus = Person("Ink Sen");
        Join(host, zeus, sessionId);
        SetRole(host, zeus, StaffRole.Zeus);
        revision = Clear(host, zeus, mortal.Id, revision);

        HostResult asGod = Complete(host, Person("Furia Bloom"), mortal.Id, revision);
        Assert.False(asGod.Ok);

        HostResult done = Complete(host, fate, mortal.Id, revision);
        Assert.True(done.Ok, done.Reason);
        Participant finished = done.Snapshot!.Participant(mortal.Id)!;
        Assert.Equal(2, finished.Level);
        Assert.Equal(4, finished.Threads);
        Assert.Null(finished.Strength);
        Assert.False(finished.Completed);

        HostResult second = Complete(host, fate, mortal.Id, finished.Revision);
        Assert.False(second.Ok);
    }

    [Fact]
    public void Protocol_roles_round_trip_as_camel_case()
    {
        string json = JsonSerializer.Serialize(new ProtocolMessage
        {
            Type = MessageType.RoleSet,
            Role = StaffRole.Aphrodite,
        }, ProtocolJson.Options);

        Assert.Contains("\"aphrodite\"", json, StringComparison.Ordinal);
        ProtocolMessage? back = JsonSerializer.Deserialize<ProtocolMessage>(json, ProtocolJson.Options);
        Assert.Equal(StaffRole.Aphrodite, back?.Role);
    }

    [Fact]
    public void Removing_a_player_requires_the_session_password()
    {
        (SessionHost host, _, Participant mortal) = SeatedMortal();
        HostResult denied = host.Dispatch(Person("Furia Bloom"), new ProtocolMessage
        {
            Type = MessageType.ParticipantRemove,
            ParticipantId = mortal.Id,
            Password = "wrong",
        }, Now);
        Assert.False(denied.Ok);
        Assert.NotNull(denied.Snapshot!.Participant(mortal.Id));

        HostResult removed = host.Dispatch(Person("Furia Bloom"), new ProtocolMessage
        {
            Type = MessageType.ParticipantRemove,
            ParticipantId = mortal.Id,
            Password = "secret",
        }, Now);
        Assert.True(removed.Ok);
        Assert.Null(removed.Snapshot!.Participant(mortal.Id));
    }

    private static long Clear(SessionHost host, CharacterIdentity god, string participantId, long revision)
    {
        StaffRole role = host.Dispatch(god, new ProtocolMessage { Type = MessageType.Heartbeat }, Now)
            .Snapshot!.Member(god.Id)!.Role;
        RollBoth(host, god, participantId, StaffText.AspectOf(role)!.Value);
        HostResult passed = Pass(host, god, participantId, revision);
        Assert.True(passed.Ok, passed.Reason);
        host.Dispatch(god, new ProtocolMessage
        {
            Type = MessageType.EditEnd,
            Scope = EditScope.Trial(participantId),
        }, Now);
        return passed.Snapshot!.Participant(participantId)!.Revision;
    }

    private static void RollBoth(SessionHost host, CharacterIdentity god, string participantId, TrialAspect aspect)
    {
        int sides = aspect == TrialAspect.Power ? 8 : 6;
        HostResult player = Dice(host, god, participantId, "player", [6, 5, 4], BoardRevision(host, god, participantId));
        Assert.True(player.Ok, player.Reason);
        HostResult gods = Dice(host, god, participantId, "god", [1, 1, Math.Min(2, sides)], BoardRevision(host, god, participantId));
        Assert.True(gods.Ok, gods.Reason);
    }

    private static long BoardRevision(SessionHost host, CharacterIdentity who, string participantId) =>
        host.Dispatch(who, new ProtocolMessage { Type = MessageType.Heartbeat }, Now).Snapshot!.Board(participantId)?.Revision ?? 0;

    private static (SessionHost Host, Participant Mortal) AresTrial()
    {
        (SessionHost host, _, Participant mortal) = SeatedMortal();
        SetRole(host, Person("Furia Bloom"), StaffRole.Ares);
        Assert.True(Dice(host, Person("Furia Bloom"), mortal.Id, "player", [6, 6, 6], 0).Ok);
        Assert.True(Dice(host, Person("Furia Bloom"), mortal.Id, "god", [1, 1, 1], 1).Ok);
        return (host, mortal);
    }

    private static (SessionHost Host, string SessionId, Participant Mortal) SeatedMortal()
    {
        (SessionHost host, string sessionId) = Open();
        SetRole(host, Person("Furia Bloom"), StaffRole.Fate);
        HostResult registered = host.Dispatch(Person("Furia Bloom"), new ProtocolMessage
        {
            Type = MessageType.ParticipantRegister,
            FirstName = "Lulu",
            LastName = "Pillow",
            World = "Phoenix",
            Discord = "lulu",
            Threads = 4,
            Level = 100,
        }, Now);
        Assert.True(registered.Ok);
        return (host, sessionId, registered.Snapshot!.Participants.Single());
    }

    private static (SessionHost Host, string SessionId) Open()
    {
        var host = new SessionHost();
        HostResult created = host.Dispatch(Person("Furia Bloom"), new ProtocolMessage
        {
            Type = MessageType.SessionCreate,
            SessionName = "Night",
            Password = "secret",
        }, Now);
        Assert.True(created.Ok);
        return (host, created.Snapshot!.Id);
    }

    private static void Join(SessionHost host, CharacterIdentity who, string sessionId)
    {
        HostResult joined = host.Dispatch(who, new ProtocolMessage
        {
            Type = MessageType.SessionJoin,
            SessionId = sessionId,
        }, Now);
        Assert.True(joined.Ok);
    }

    private static HostResult SetRole(SessionHost host, CharacterIdentity who, StaffRole role) =>
        host.Dispatch(who, new ProtocolMessage { Type = MessageType.RoleSet, Role = role }, Now);

    private static HostResult SetThreads(SessionHost host, CharacterIdentity who, string participantId, int threads, long revision) =>
        host.Dispatch(who, new ProtocolMessage
        {
            Type = MessageType.ThreadsSet,
            ParticipantId = participantId,
            Threads = threads,
            Revision = revision,
        }, Now);

    private static HostResult Dice(SessionHost host, CharacterIdentity who, string participantId, string side, List<int> values, long revision) =>
        host.Dispatch(who, new ProtocolMessage
        {
            Type = MessageType.TrialDice,
            ParticipantId = participantId,
            Side = side,
            Values = values,
            Revision = revision,
        }, Now);

    private static HostResult Pass(SessionHost host, CharacterIdentity who, string participantId, long revision) =>
        host.Dispatch(who, new ProtocolMessage
        {
            Type = MessageType.TrialPass,
            ParticipantId = participantId,
            Revision = revision,
        }, Now);

    private static HostResult Complete(SessionHost host, CharacterIdentity who, string participantId, long revision) =>
        host.Dispatch(who, new ProtocolMessage
        {
            Type = MessageType.ParticipantComplete,
            ParticipantId = participantId,
            Revision = revision,
        }, Now);

    private static CharacterIdentity Person(string name) => new(name, "Phoenix");
}
