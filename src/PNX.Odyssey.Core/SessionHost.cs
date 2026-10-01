using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace Pnx.Odyssey.Core;

public sealed class HostResult
{
    public bool Ok { get; init; }

    public string? Reason { get; init; }

    public string? SavedBy { get; init; }

    public SessionSnapshot? Snapshot { get; init; }

    public IReadOnlyList<SessionSummary> Sessions { get; init; } = [];
}

/// <summary>
/// In-memory session authority used by the local loopback and by tests.
/// A production server should apply the same rules.
/// </summary>
public sealed class SessionHost
{
    public const int LockSeconds = 20;
    public const int PresenceSeconds = 25;
    public const int MaxRolls = 30;

    private readonly Dictionary<string, ClientSlot> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LiveSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public HostResult Dispatch(CharacterIdentity identity, ProtocolMessage message, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(message);

        ClientSlot client = Touch(identity, utcNow);
        Sweep(utcNow);
        return message.Type switch
        {
            MessageType.Hello => Greet(client, utcNow),
            MessageType.Heartbeat => Ok(client),
            MessageType.SessionCreate => Create(client, message, utcNow),
            MessageType.SessionJoin => Join(client, message, utcNow),
            MessageType.SessionClose => Close(client, message),
            MessageType.SessionLeave => Leave(client),
            MessageType.RoleSet => SetRole(client, message),
            MessageType.ParticipantRegister => Register(client, message),
            MessageType.ParticipantRemove => Remove(client, message),
            MessageType.ParticipantLevel => UpdateLevel(client, message),
            MessageType.ParticipantComplete => Complete(client, message),
            MessageType.ParticipantEdit => EditParticipant(client, message),
            MessageType.ThreadsSet => SetThreads(client, message),
            MessageType.EditBegin => BeginEdit(client, message, utcNow),
            MessageType.EditEnd => EndEdit(client, message),
            MessageType.TrialDice => RecordDice(client, message, utcNow),
            MessageType.TrialFail => Fail(client, message),
            MessageType.TrialPass => Pass(client, message),
            MessageType.LobbyChat => Chat(client, message, utcNow),
            MessageType.AspectClaim => ClaimAspect(client, message, utcNow),
            MessageType.AspectRemove => RemoveAspect(client, message),
            MessageType.GodMacroSave => SaveGodMacro(client, message),
            MessageType.GodMacroRemove => RemoveGodMacro(client, message),
            _ => Reject(client, "Unknown message."),
        };
    }

    public HostResult Peek(CharacterIdentity identity, DateTime utcNow)
    {
        Sweep(utcNow);
        if (!_clients.TryGetValue(identity.Id, out ClientSlot? client))
        {
            return new HostResult { Ok = true, Sessions = ListSessions() };
        }

        return Ok(client);
    }

    public void Disconnect(CharacterIdentity identity, DateTime utcNow)
    {
        if (_clients.TryGetValue(identity.Id, out ClientSlot? client) && client.Member != null)
            client.Member.LastSeen = utcNow.AddSeconds(-(PresenceSeconds + 1));

        Sweep(utcNow);
    }

    public HostArchive Capture() => new()
    {
        Sessions = _sessions.Values.Select(session => new ArchivedSession
        {
            Id = session.Id,
            Name = session.Name,
            PasswordHash = session.PasswordHash,
            CreatedAt = session.CreatedAt,
            Revision = session.Revision,
            LastEditor = session.LastEditor,
            Members = session.Members.Values.Select(member => member.Clone()).ToList(),
            Participants = session.Participants.Values.Select(participant => participant.Clone()).ToList(),
            Locks = session.Locks.Values.Select(edit => edit.Clone()).ToList(),
            Boards = session.Boards.Values.Select(board => board.Clone()).ToList(),
            Log = session.Log.Select(line => line.Clone()).ToList(),
            Claims = session.Claims.Select(claim => claim.Clone()).ToList(),
            Macros = session.Macros.Select(macro => macro.Clone()).ToList(),
        }).ToList(),
    };

    public void Restore(HostArchive archive)
    {
        _clients.Clear();
        _sessions.Clear();
        foreach (ArchivedSession saved in archive.Sessions)
        {
            var session = new LiveSession
            {
                Id = saved.Id,
                Name = saved.Name,
                PasswordHash = saved.PasswordHash,
                CreatedAt = saved.CreatedAt,
                Revision = saved.Revision,
                LastEditor = saved.LastEditor,
            };
            foreach (SessionMember member in saved.Members)
            {
                SessionMember copy = member.Clone();
                copy.Connected = false;
                copy.LastSeen = DateTime.MinValue;
                session.Members[copy.Id] = copy;
            }

            foreach (Participant participant in saved.Participants)
                session.Participants[participant.Id] = participant.Clone();
            foreach (EditLock edit in saved.Locks)
                session.Locks[edit.Scope] = edit.Clone();
            foreach (TrialBoard board in saved.Boards)
                session.Boards[board.ParticipantId] = board.Clone();
            session.Log.AddRange(saved.Log.Select(line => line.Clone()));
            foreach (AspectClaim claim in saved.Claims ?? [])
                session.Claims.Add(claim.Clone());
            foreach (GodMacro macro in saved.Macros ?? [])
                session.Macros.Add(macro.Clone());
            _sessions[session.Id] = session;
        }
    }

    private HostResult Greet(ClientSlot client, DateTime utcNow)
    {
        if (client.Session == null)
        {
            LiveSession? seated = _sessions.Values.FirstOrDefault(session => session.Members.ContainsKey(client.Identity.Id));
            if (seated != null)
                Enter(client, seated, utcNow);
        }

        return Ok(client);
    }

    private HostResult Create(ClientSlot client, ProtocolMessage message, DateTime utcNow)
    {
        string name = Clean(message.SessionName, 40);
        if (name.Length == 0)
            return Reject(client, "Name the session.");
        if (string.IsNullOrEmpty(message.Password))
            return Reject(client, "A close password is required.");

        Leave(client);
        var session = new LiveSession
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = name,
            PasswordHash = Hash(message.Password),
            CreatedAt = utcNow,
        };
        _sessions.Add(session.Id, session);
        Enter(client, session, utcNow);
        return Ok(client);
    }

    private HostResult Join(ClientSlot client, ProtocolMessage message, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(message.SessionId) || !_sessions.TryGetValue(message.SessionId, out LiveSession? session))
            return Reject(client, "That session is no longer open.");

        if (client.Session != null && !string.Equals(client.Session.Id, session.Id, StringComparison.Ordinal))
            Leave(client);

        Enter(client, session, utcNow);
        return Ok(client);
    }

    private HostResult Close(ClientSlot client, ProtocolMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.SessionId) || !_sessions.TryGetValue(message.SessionId, out LiveSession? session))
            return Reject(client, "That session is no longer open.");
        if (string.IsNullOrEmpty(message.Password) || !PasswordMatches(session.PasswordHash, message.Password))
            return Reject(client, "The close password does not match.");

        _sessions.Remove(session.Id);
        foreach (ClientSlot seated in _clients.Values)
        {
            if (seated.Session == session)
                seated.Session = null;
        }

        return Ok(client);
    }

    private HostResult Leave(ClientSlot client)
    {
        if (client.Session != null && client.Member != null)
        {
            Note(client.Session, client.Member, LobbyKind.Leave, "Left the lobby");
            client.Session.Members.Remove(client.Identity.Id);
            client.Session = null;
        }

        return Ok(client);
    }

    private HostResult SetRole(ClientSlot client, ProtocolMessage message)
    {
        if (client.Session == null || client.Member == null)
            return Reject(client, "Join a session before choosing a role.");
        if (message.Role == null)
            return Reject(client, "Choose a role.");

        StaffRole role = message.Role.Value;
        if (StaffText.IsExclusive(role))
        {
            SessionMember? taken = client.Session.Members.Values.FirstOrDefault(member =>
                member.Connected
                && member.Role == role
                && !string.Equals(member.Id, client.Identity.Id, StringComparison.OrdinalIgnoreCase));
            if (taken != null)
                return Reject(client, $"{StaffText.Label(role)} is already assigned to {taken.Name}.");
        }

        StaffRole previous = client.Member.Role;
        client.Member.Role = role;
        ReleaseRoleConflicts(client.Session, role, client.Identity.Id);
        if (previous != role)
            Note(client.Session, client.Member, LobbyKind.Role, $"Took the {StaffText.Label(role)} seat");
        return Ok(client);
    }

    private HostResult Register(ClientSlot client, ProtocolMessage message)
    {
        if (!RequireStaff(client, out LiveSession? session, out SessionMember? member, out HostResult? failure))
            return failure!;

        string first = Clean(message.FirstName, 32);
        string last = Clean(message.LastName, 32);
        string world = Clean(message.World, 32);
        if (first.Length == 0 || last.Length == 0 || world.Length == 0)
            return Reject(client, "First name, last name, and world are required.");

        int threads = message.Threads ?? TrialRules.StartingThreads;
        if (threads < TrialRules.MinStartingThreads || threads > TrialRules.MaxThreads)
            return Reject(client, "Starting threads must be from 1 to 10.");

        string id = ParticipantIds.Create(first, last, world);
        if (session!.Participants.ContainsKey(id))
            return Reject(client, "Already registered.");

        session.Participants.Add(id, new Participant
        {
            Id = id,
            FirstName = first,
            LastName = last,
            World = world,
            Discord = EmptyToNull(Clean(message.Discord, 64)),
            Level = 1,
            Threads = threads,
            Revision = 1,
            LastEditor = member!.Name,
        });
        session.LastEditor = member.Name;
        session.Revision++;
        Note(session, member, LobbyKind.Register, $"Registered {first} {last} · {world} with {threads} threads");
        return Ok(client);
    }

    private HostResult Remove(ClientSlot client, ProtocolMessage message)
    {
        if (client.Session == null || client.Member == null)
            return Reject(client, "Join a session first.");
        if (string.IsNullOrEmpty(message.Password) || !PasswordMatches(client.Session.PasswordHash, message.Password))
            return Reject(client, "The session password does not match.");
        if (string.IsNullOrWhiteSpace(message.ParticipantId) || !client.Session.Participants.TryGetValue(message.ParticipantId, out Participant? gone))
            return Reject(client, "That player is not registered.");

        client.Session.Participants.Remove(message.ParticipantId);
        client.Session.Boards.Remove(message.ParticipantId);
        string[] scopes =
        [
            EditScope.Threads(message.ParticipantId),
            EditScope.Register(message.ParticipantId),
            EditScope.Trial(message.ParticipantId),
        ];
        foreach (string scope in scopes)
            client.Session.Locks.Remove(scope);
        client.Session.LastEditor = client.Member.Name;
        client.Session.Revision++;
        Note(client.Session, client.Member, LobbyKind.Remove, $"Removed {gone.FullName}");
        return Ok(client);
    }

    private HostResult UpdateLevel(ClientSlot client, ProtocolMessage message)
    {
        if (client.Session == null)
            return Reject(client, "Join a session first.");
        if (!TryParticipant(client, message.ParticipantId, out Participant? participant, out HostResult? failure))
            return failure!;

        int level = ClampLevel(message.Level ?? 0);
        if (level > 0)
            participant!.Level = level;
        return Ok(client);
    }

    private HostResult SetThreads(ClientSlot client, ProtocolMessage message)
    {
        if (!RequireFate(client, out _, out SessionMember? member, out HostResult? failure))
            return failure!;
        if (!TryParticipant(client, message.ParticipantId, out Participant? participant, out failure))
            return failure!;
        if (message.Threads == null)
            return Reject(client, "A thread count is required.");

        int threads = message.Threads.Value;
        if (threads < 0 || threads > TrialRules.MaxThreads)
            return Reject(client, "Threads must stay between 0 and 10.");
        if (!RevisionMatches(participant!, message.Revision))
            return Stale(client, participant!);

        int before = participant!.Threads;
        participant.Threads = threads;
        participant.Revision++;
        participant.LastEditor = member!.Name;
        client.Session!.LastEditor = member.Name;
        client.Session.Revision++;
        Note(client.Session, member, LobbyKind.Threads, $"{participant.FullName}  {before} → {threads} threads");
        return Ok(client);
    }

    private HostResult Complete(ClientSlot client, ProtocolMessage message)
    {
        if (!RequireFate(client, out _, out SessionMember? member, out HostResult? failure))
            return failure!;
        if (!TryParticipant(client, message.ParticipantId, out Participant? participant, out failure))
            return failure!;
        if (!TrialRules.AllTrialsCleared(participant))
            return Reject(client, "All four trials must be cleared.");
        if (!RevisionMatches(participant, message.Revision))
            return Stale(client, participant);

        participant.Strength = null;
        participant.Harmony = null;
        participant.Fear = null;
        participant.Power = null;
        participant.Completed = false;
        participant.Level = Math.Max(participant.Level, 1) + 1;
        participant.Revision++;
        participant.LastEditor = member!.Name;
        if (client.Session!.Boards.TryGetValue(participant.Id, out TrialBoard? board))
        {
            board.PlayerRolls.Clear();
            board.GodRolls.Clear();
            board.Revision++;
        }

        client.Session.LastEditor = member.Name;
        client.Session.Revision++;
        Note(client.Session, member, LobbyKind.Run, $"{participant.FullName} started run {participant.Level}");
        return Ok(client);
    }

    private HostResult EditParticipant(ClientSlot client, ProtocolMessage message)
    {
        if (!RequireFate(client, out LiveSession? session, out SessionMember? member, out HostResult? failure))
            return failure!;
        if (!TryParticipant(client, message.ParticipantId, out Participant? participant, out failure))
            return failure!;
        if (!RevisionMatches(participant, message.Revision))
            return Stale(client, participant);

        string first = Clean(message.FirstName, 32);
        string last = Clean(message.LastName, 32);
        string world = Clean(message.World, 32);
        if (first.Length == 0 || last.Length == 0 || world.Length == 0)
            return Reject(client, "First name, last name, and world are required.");
        if (message.Threads == null)
            return Reject(client, "A thread count is required.");

        int threads = message.Threads.Value;
        if (threads < 0 || threads > TrialRules.MaxThreads)
            return Reject(client, "Threads must stay between 0 and 10.");

        if (!ValidVictor(message.Strength, TrialAspect.Strength)
            || !ValidVictor(message.Harmony, TrialAspect.Harmony)
            || !ValidVictor(message.Fear, TrialAspect.Fear)
            || !ValidVictor(message.Power, TrialAspect.Power))
            return Reject(client, "That seat does not run this trial.");

        string oldId = participant.Id;
        string newId = ParticipantIds.Create(first, last, world);
        if (!string.Equals(oldId, newId, StringComparison.Ordinal) && session!.Participants.ContainsKey(newId))
            return Reject(client, "Already registered.");

        participant.FirstName = first;
        participant.LastName = last;
        participant.World = world;
        participant.Threads = threads;
        participant.Strength = EmptyVictor(message.Strength);
        participant.Harmony = EmptyVictor(message.Harmony);
        participant.Fear = EmptyVictor(message.Fear);
        participant.Power = EmptyVictor(message.Power);
        participant.Revision++;
        participant.LastEditor = member!.Name;
        if (!string.Equals(oldId, newId, StringComparison.Ordinal))
        {
            session!.Participants.Remove(oldId);
            participant.Id = newId;
            session.Participants[newId] = participant;
            if (session.Boards.Remove(oldId, out TrialBoard? board))
            {
                board.ParticipantId = newId;
                session.Boards[newId] = board;
            }

            foreach (AspectClaim claim in session.Claims)
            {
                if (!string.Equals(claim.ParticipantId, oldId, StringComparison.OrdinalIgnoreCase))
                    continue;
                claim.ParticipantId = newId;
                claim.PlayerName = participant.FullName;
            }
        }

        session!.LastEditor = member.Name;
        session.Revision++;
        return Ok(client);
    }

    private static bool ValidVictor(StaffRole? role, TrialAspect aspect) =>
        role is null or StaffRole.None || StaffText.AspectOf(role.Value) == aspect;

    private static StaffRole? EmptyVictor(StaffRole? role) => role is null or StaffRole.None ? null : role;

    private HostResult BeginEdit(ClientSlot client, ProtocolMessage message, DateTime utcNow)
    {
        if (client.Session == null || client.Member == null)
            return Reject(client, "Join a session first.");

        string scope = Clean(message.Scope, 80);
        if (scope.Length == 0)
            return Reject(client, "An edit scope is required.");

        if (client.Session.Locks.TryGetValue(scope, out EditLock? existing)
            && existing.ExpiresAt > utcNow
            && !string.Equals(existing.HolderId, client.Member.Id, StringComparison.OrdinalIgnoreCase))
        {
            return Ok(client);
        }

        if (scope.StartsWith("trial:", StringComparison.Ordinal) && StaffText.AspectOf(client.Member.Role) is { } aspect)
        {
            string participantId = scope["trial:".Length..];
            AlignBoard(client.Session, participantId, aspect, resetIfDifferent: true);
        }

        client.Session.Locks[scope] = new EditLock
        {
            Scope = scope,
            HolderId = client.Member.Id,
            HolderName = client.Member.Name,
            ExpiresAt = utcNow.AddSeconds(LockSeconds),
        };
        return Ok(client);
    }

    private HostResult EndEdit(ClientSlot client, ProtocolMessage message)
    {
        if (client.Session == null || client.Member == null)
            return Ok(client);

        string scope = message.Scope ?? "";
        if (client.Session.Locks.TryGetValue(scope, out EditLock? existing)
            && string.Equals(existing.HolderId, client.Member.Id, StringComparison.OrdinalIgnoreCase))
        {
            client.Session.Locks.Remove(scope);
        }

        return Ok(client);
    }

    private HostResult RecordDice(ClientSlot client, ProtocolMessage message, DateTime utcNow)
    {
        if (!RequireGod(client, out LiveSession? session, out SessionMember? member, out TrialAspect? aspect, out HostResult? failure))
            return failure!;
        if (!TryParticipant(client, message.ParticipantId, out Participant? participant, out failure))
            return failure!;

        string side = message.Side ?? "";
        if (side is not ("player" or "god"))
            return Reject(client, "Dice belong to the player or the god.");

        List<int> incoming = message.Values ?? [];
        if (incoming.Count > MaxRolls)
            return Reject(client, "Dice history is empty or too long.");

        string scope = EditScope.Trial(participant!.Id);
        if (session!.Locks.TryGetValue(scope, out EditLock? edit)
            && edit.ExpiresAt > utcNow
            && !string.Equals(edit.HolderId, member!.Id, StringComparison.OrdinalIgnoreCase))
        {
            return Reject(client, $"{edit.HolderName} is running this trial.", edit.HolderName);
        }

        TrialBoard board = AlignBoard(session, participant.Id, aspect!.Value, resetIfDifferent: false);
        bool reset = board.Aspect != aspect.Value;
        if (reset)
        {
            board.Aspect = aspect.Value;
            board.PlayerRolls.Clear();
            board.GodRolls.Clear();
        }

        if (incoming.Count == 0)
        {
            if (!reset && message.Revision != board.Revision)
                return StaleBoard(client, board);
            if (side == "god")
                board.GodRolls = [];
            else
                board.PlayerRolls = [];
            return TouchBoard(client, session, board, member!, scope, utcNow);
        }

        int sides = side == "god" ? TrialRules.DieSides(member!.Role) : TrialRules.MortalSides;
        if (incoming.Any(value => value < 1 || value > sides))
            return Reject(client, "A die was outside the allowed range.");

        List<int> current = side == "god" ? board.GodRolls : board.PlayerRolls;
        bool revisionOk = reset || message.Revision == board.Revision;
        bool append = current.Count > 0
            && current.Count < incoming.Count
            && incoming.Take(current.Count).SequenceEqual(current);
        if (!revisionOk && !append)
            return StaleBoard(client, board);

        if (side == "god")
            board.GodRolls = TrimRolls(incoming);
        else
            board.PlayerRolls = TrimRolls(incoming);

        return TouchBoard(client, session, board, member!, scope, utcNow);
    }

    private HostResult TouchBoard(ClientSlot client, LiveSession session, TrialBoard board, SessionMember member, string scope, DateTime utcNow)
    {
        board.Revision++;
        board.LastEditor = member.Name;
        session.Locks[scope] = new EditLock
        {
            Scope = scope,
            HolderId = member.Id,
            HolderName = member.Name,
            ExpiresAt = utcNow.AddSeconds(LockSeconds),
        };
        session.Revision++;
        return Ok(client);
    }

    private HostResult Fail(ClientSlot client, ProtocolMessage message)
    {
        if (!TryTrial(client, message, out Participant? participant, out TrialBoard? board, out SessionMember? member, out HostResult? failure))
            return failure!;
        if (!RevisionMatches(participant!, message.Revision))
            return Stale(client, participant!);
        if (!TrialRules.CanRoll(participant, board!.Aspect))
            return Reject(client, BlockReason(participant, board.Aspect));

        int before = participant.Threads;
        participant.Threads = Math.Max(0, before - 1);
        participant.Revision++;
        participant.LastEditor = member!.Name;
        board.PlayerRolls.Clear();
        board.GodRolls.Clear();
        board.Revision++;
        board.LastEditor = member!.Name;
        client.Session!.LastEditor = member.Name;
        client.Session.Revision++;
        Note(client.Session, member, LobbyKind.Fail, $"{participant.FullName} failed {StaffText.AspectName(board.Aspect)}. {before} → {participant.Threads} threads");
        return Ok(client);
    }

    private HostResult Pass(ClientSlot client, ProtocolMessage message)
    {
        if (!TryTrial(client, message, out Participant? participant, out TrialBoard? board, out SessionMember? member, out HostResult? failure))
            return failure!;
        if (TrialRules.Victor(participant!, board!.Aspect) != null)
            return Ok(client);
        if (!RevisionMatches(participant, message.Revision))
            return Stale(client, participant);
        if (!TrialRules.CanRoll(participant, board.Aspect))
            return Reject(client, BlockReason(participant, board.Aspect));

        SetVictor(participant, board.Aspect, member!.Role);
        participant.Revision++;
        participant.LastEditor = member.Name;
        board.PlayerRolls.Clear();
        board.GodRolls.Clear();
        board.Revision++;
        board.LastEditor = member.Name;
        client.Session!.LastEditor = member.Name;
        client.Session.Revision++;
        Note(client.Session, member, LobbyKind.Pass, $"{participant.FullName} claimed {StaffText.AspectName(board.Aspect)} · {StaffText.Label(member.Role)}");
        return Ok(client);
    }

    private HostResult Chat(ClientSlot client, ProtocolMessage message, DateTime utcNow)
    {
        if (client.Session == null || client.Member == null)
            return Reject(client, "Join a session before chatting.");

        string text = Clean(message.Text, 240);
        if (text.Length == 0)
            return Reject(client, "Say something first.");

        Note(client.Session, client.Member, LobbyKind.Chat, text, utcNow);
        client.Session.Revision++;
        return Ok(client);
    }

    private HostResult ClaimAspect(ClientSlot client, ProtocolMessage message, DateTime utcNow)
    {
        if (!RequireFate(client, out LiveSession? session, out SessionMember? member, out HostResult? failure))
            return failure!;
        if (!TryParticipant(client, message.ParticipantId, out Participant? participant, out failure))
            return failure!;

        AspectCatalog.Offering? offering = AspectCatalog.Find(message.OfferingId);
        if (offering == null)
            return Reject(client, "That aspect is not in the list.");
        if (!AspectCatalog.HasRoom(offering, session!.Claims))
            return Reject(client, "No claims left for that aspect.");

        var claim = new AspectClaim
        {
            Id = Guid.NewGuid().ToString("N"),
            OfferingId = offering.Id,
            ParticipantId = participant!.Id,
            PlayerName = participant.FullName,
            Run = Math.Max(participant.Level, 1),
            At = utcNow,
        };
        session.Claims.Add(claim);
        session.Revision++;
        Note(session, member!, LobbyKind.Claim, AspectCatalog.ClaimLog(claim), utcNow);
        return Ok(client);
    }

    private HostResult RemoveAspect(ClientSlot client, ProtocolMessage message)
    {
        if (!RequireFate(client, out LiveSession? session, out _, out HostResult? failure))
            return failure!;
        if (string.IsNullOrEmpty(message.Password) || !PasswordMatches(session!.PasswordHash, message.Password))
            return Reject(client, "The session password does not match.");

        AspectClaim? claim = session.Claims.FirstOrDefault(item => item.Id == message.ClaimId);
        if (claim == null)
            return Reject(client, "That claim is already gone.");

        session.Claims.Remove(claim);
        session.Revision++;
        return Ok(client);
    }

    private HostResult SaveGodMacro(ClientSlot client, ProtocolMessage message)
    {
        if (!RequireGod(client, out LiveSession? session, out SessionMember? member, out _, out HostResult? failure))
            return failure!;

        string name = Clean(message.Name, 40);
        string text = message.Text?.Replace('\r', '\n').Trim() ?? "";
        if (name.Length == 0 || text.Length == 0)
            return Reject(client, "Name the macro and write its lines.");
        if (text.Length > 900)
            return Reject(client, "That macro is too long.");

        if (string.IsNullOrWhiteSpace(message.MacroId))
        {
            session!.Macros.Add(new GodMacro
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Text = text,
                AuthorId = member!.Id,
                AuthorName = member.Name,
            });
        }
        else
        {
            GodMacro? existing = session!.Macros.FirstOrDefault(macro => macro.Id == message.MacroId);
            if (existing == null)
                return Reject(client, "That macro is already gone.");
            if (!string.Equals(existing.AuthorId, member!.Id, StringComparison.OrdinalIgnoreCase))
                return Reject(client, "Only the person who wrote that macro can change it.");
            existing.Name = name;
            existing.Text = text;
        }

        session.Revision++;
        return Ok(client);
    }

    private HostResult RemoveGodMacro(ClientSlot client, ProtocolMessage message)
    {
        if (!RequireGod(client, out LiveSession? session, out SessionMember? member, out _, out HostResult? failure))
            return failure!;

        GodMacro? existing = session!.Macros.FirstOrDefault(macro => macro.Id == message.MacroId);
        if (existing == null)
            return Reject(client, "That macro is already gone.");
        if (!string.Equals(existing.AuthorId, member!.Id, StringComparison.OrdinalIgnoreCase))
            return Reject(client, "Only the person who wrote that macro can delete it.");

        session.Macros.Remove(existing);
        session.Revision++;
        return Ok(client);
    }

    private static void Note(LiveSession session, SessionMember member, LobbyKind kind, string text, DateTime? at = null)
    {
        session.Log.Add(new LobbyLine
        {
            At = at ?? DateTime.UtcNow,
            Name = member.Name,
            Role = member.Role,
            Kind = kind,
            Text = text,
        });
        if (session.Log.Count > 200)
            session.Log.RemoveRange(0, session.Log.Count - 200);
    }

    private bool TryTrial(
        ClientSlot client,
        ProtocolMessage message,
        [NotNullWhen(true)] out Participant? participant,
        [NotNullWhen(true)] out TrialBoard? board,
        [NotNullWhen(true)] out SessionMember? member,
        [NotNullWhen(false)] out HostResult? failure)
    {
        participant = null;
        board = null;
        member = client.Member;
        if (!RequireGod(client, out LiveSession? session, out member, out TrialAspect? aspect, out failure))
            return false;
        if (!TryParticipant(client, message.ParticipantId, out participant, out failure))
            return false;

        if (!session.Boards.TryGetValue(participant.Id, out board) || board.Aspect != aspect)
            board = AlignBoard(session, participant.Id, aspect.Value, resetIfDifferent: true);

        failure = null;
        return true;
    }

    private static TrialBoard AlignBoard(LiveSession session, string participantId, TrialAspect aspect, bool resetIfDifferent)
    {
        if (!session.Boards.TryGetValue(participantId, out TrialBoard? board))
        {
            board = new TrialBoard { ParticipantId = participantId, Aspect = aspect };
            session.Boards.Add(participantId, board);
            return board;
        }

        if (resetIfDifferent && board.Aspect != aspect)
        {
            board.Aspect = aspect;
            board.PlayerRolls.Clear();
            board.GodRolls.Clear();
            board.Revision++;
        }

        return board;
    }

    private static void SetVictor(Participant participant, TrialAspect aspect, StaffRole role)
    {
        switch (aspect)
        {
            case TrialAspect.Strength:
                participant.Strength = role;
                break;
            case TrialAspect.Harmony:
                participant.Harmony = role;
                break;
            case TrialAspect.Fear:
                participant.Fear = role;
                break;
            case TrialAspect.Power:
                participant.Power = role;
                break;
        }
    }

    private static string BlockReason(Participant participant, TrialAspect aspect)
    {
        if (participant.Threads < 1)
            return "A shade cannot take a trial.";
        if (aspect == TrialAspect.Power)
            return "Zeus can be faced after the first three trials.";
        return "That trial is not open.";
    }

    private void Enter(ClientSlot client, LiveSession session, DateTime utcNow)
    {
        client.Session = session;
        bool joined = !session.Members.ContainsKey(client.Identity.Id);
        if (!session.Members.TryGetValue(client.Identity.Id, out SessionMember? member))
        {
            member = new SessionMember
            {
                Id = client.Identity.Id,
                Name = client.Identity.Name,
                World = client.Identity.World,
            };
            session.Members.Add(member.Id, member);
        }

        member.Name = client.Identity.Name;
        member.World = client.Identity.World;
        member.Connected = true;
        member.LastSeen = utcNow;
        client.Member = member;
        if (StaffText.IsExclusive(member.Role)
            && session.Members.Values.Any(other => other.Connected
                && other.Role == member.Role
                && !string.Equals(other.Id, member.Id, StringComparison.OrdinalIgnoreCase)))
        {
            member.Role = StaffRole.None;
        }

        if (joined)
            Note(session, member, LobbyKind.Join, "Joined the lobby", utcNow);
    }

    private static void ReleaseRoleConflicts(LiveSession session, StaffRole role, string holderId)
    {
        if (!StaffText.IsExclusive(role))
            return;

        foreach (SessionMember member in session.Members.Values)
        {
            if (member.Role == role && !string.Equals(member.Id, holderId, StringComparison.OrdinalIgnoreCase))
                member.Role = StaffRole.None;
        }
    }

    private ClientSlot Touch(CharacterIdentity identity, DateTime utcNow)
    {
        if (!_clients.TryGetValue(identity.Id, out ClientSlot? client))
        {
            client = new ClientSlot(identity);
            _clients.Add(identity.Id, client);
        }

        client.LastSeen = utcNow;
        if (client.Member != null)
        {
            client.Member.LastSeen = utcNow;
            client.Member.Connected = true;
            client.Member.Name = identity.Name;
        }

        return client;
    }

    private void Sweep(DateTime utcNow)
    {
        foreach (LiveSession session in _sessions.Values)
        {
            foreach (SessionMember member in session.Members.Values)
                member.Connected = utcNow - member.LastSeen <= TimeSpan.FromSeconds(PresenceSeconds);

            List<string> expired = session.Locks
                .Where(pair => pair.Value.ExpiresAt <= utcNow)
                .Select(pair => pair.Key)
                .ToList();
            foreach (string scope in expired)
                session.Locks.Remove(scope);
        }
    }

    private bool RequireStaff(ClientSlot client, out LiveSession? session, out SessionMember? member, out HostResult? failure)
    {
        session = client.Session;
        member = client.Member;
        if (session == null || member == null)
        {
            failure = Reject(client, "Join a session first.");
            return false;
        }

        if (member.Role == StaffRole.None)
        {
            failure = Reject(client, "Choose a role before registering.");
            return false;
        }

        failure = null;
        return true;
    }

    private bool RequireFate(ClientSlot client, out LiveSession? session, out SessionMember? member, out HostResult? failure)
    {
        if (!RequireStaff(client, out session, out member, out failure))
            return false;
        if (!StaffText.RunsTable(member!.Role))
        {
            failure = Reject(client, "Only Fate or the Director can do that.");
            return false;
        }

        return true;
    }

    private bool RequireGod(
        ClientSlot client,
        [NotNullWhen(true)] out LiveSession? session,
        [NotNullWhen(true)] out SessionMember? member,
        [NotNullWhen(true)] out TrialAspect? aspect,
        [NotNullWhen(false)] out HostResult? failure)
    {
        session = client.Session;
        member = client.Member;
        aspect = member == null ? null : StaffText.AspectOf(member.Role);
        if (session == null || member == null)
        {
            failure = Reject(client, "Join a session first.");
            return false;
        }

        if (aspect == null)
        {
            failure = Reject(client, "Only a god can run a trial.");
            return false;
        }

        failure = null;
        return true;
    }

    private bool TryParticipant(
        ClientSlot client,
        string? id,
        [NotNullWhen(true)] out Participant? participant,
        [NotNullWhen(false)] out HostResult? failure)
    {
        participant = null;
        if (client.Session == null)
        {
            failure = Reject(client, "Join a session first.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(id) || !client.Session.Participants.TryGetValue(id, out participant))
        {
            failure = Reject(client, "That player is not registered.");
            return false;
        }

        failure = null;
        return true;
    }

    private static bool RevisionMatches(Participant participant, long? revision) => revision == participant.Revision;

    private HostResult Stale(ClientSlot client, Participant participant)
    {
        string editor = string.IsNullOrEmpty(participant.LastEditor) ? "Someone" : participant.LastEditor;
        return Reject(client, $"{editor} saved first.", editor);
    }

    private HostResult StaleBoard(ClientSlot client, TrialBoard board)
    {
        string editor = string.IsNullOrEmpty(board.LastEditor) ? "Someone" : board.LastEditor;
        return Reject(client, $"{editor} saved first.", editor);
    }

    private HostResult Ok(ClientSlot client) => new()
    {
        Ok = true,
        Snapshot = SnapshotOf(client),
        Sessions = ListSessions(),
    };

    private HostResult Reject(ClientSlot client, string reason, string? savedBy = null) => new()
    {
        Ok = false,
        Reason = reason,
        SavedBy = savedBy,
        Snapshot = SnapshotOf(client),
        Sessions = ListSessions(),
    };

    private SessionSnapshot? SnapshotOf(ClientSlot client)
    {
        if (client.Session == null)
            return null;

        LiveSession session = client.Session;
        return new SessionSnapshot
        {
            Id = session.Id,
            Name = session.Name,
            CreatedAt = session.CreatedAt,
            Revision = session.Revision,
            Members = session.Members.Values
                .OrderBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
                .Select(member => member.Clone())
                .ToList(),
            Participants = session.Participants.Values
                .OrderBy(participant => participant.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(participant => participant.Clone())
                .ToList(),
            Locks = session.Locks.Values.Select(edit => edit.Clone()).ToList(),
            Boards = session.Boards.Values.Select(board => board.Clone()).ToList(),
            Log = session.Log.Select(line => line.Clone()).ToList(),
            Claims = session.Claims
                .OrderBy(claim => claim.At)
                .Select(claim => claim.Clone())
                .ToList(),
            Macros = session.Macros
                .OrderBy(macro => macro.Name, StringComparer.OrdinalIgnoreCase)
                .Select(macro => macro.Clone())
                .ToList(),
        };
    }

    private List<SessionSummary> ListSessions() => _sessions.Values
        .OrderByDescending(session => session.CreatedAt)
        .Select(session => new SessionSummary
        {
            Id = session.Id,
            Name = session.Name,
            CreatedAt = session.CreatedAt,
            LiveCount = session.Members.Values.Count(member => member.Connected),
        })
        .ToList();

    private static List<int> TrimRolls(List<int> incoming)
    {
        var rolls = new List<int>(incoming);
        while (rolls.Count > MaxRolls)
            rolls.RemoveRange(0, TrialRules.DicePerSet);
        return rolls;
    }

    private static int ClampLevel(int level) => Math.Clamp(level, 0, 100);

    private static string Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string cleaned = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return cleaned.Length <= max ? cleaned : cleaned[..max];
    }

    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    private static string Hash(string password)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    private static bool PasswordMatches(string hash, string password)
    {
        byte[] actual = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        byte[] expected = Convert.FromHexString(hash);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private sealed class ClientSlot(CharacterIdentity identity)
    {
        public CharacterIdentity Identity { get; } = identity;

        public DateTime LastSeen { get; set; }

        public LiveSession? Session { get; set; }

        public SessionMember? Member { get; set; }
    }

    private sealed class LiveSession
    {
        public string Id { get; set; } = "";

        public string Name { get; set; } = "";

        public string PasswordHash { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public long Revision { get; set; }

        public string LastEditor { get; set; } = "";

        public Dictionary<string, SessionMember> Members { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Participant> Participants { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, EditLock> Locks { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, TrialBoard> Boards { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<LobbyLine> Log { get; } = [];

        public List<AspectClaim> Claims { get; } = [];

        public List<GodMacro> Macros { get; } = [];
    }
}
