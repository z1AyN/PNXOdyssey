using Dalamud.Plugin.Services;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.Services;

internal sealed class OdysseyClient : IDisposable
{
    private readonly IPluginLog _log;
    private IOdysseyConnection _connection;
    private CharacterIdentity? _self;
    private string _pluginKey = "";
    private string? _activeUrl;
    private bool _usingSocket;
    private string? _heldScope;
    private DateTime _nextHoldRefresh;
    private string? _banner;
    private DateTime _bannerAt;
    private bool _left;
    private readonly List<LobbyLine> _localLog = [];
    private readonly Configuration _config;
    private SessionSnapshot? _watched;
    private string? _logSessionId;

    public IReadOnlyList<LobbyLine> Lines => _localLog;

    public OdysseyClient(Dalamud.Plugin.Services.IPluginLog log, Configuration config)
    {
        _log = log;
        _config = config;
        _connection = new LoopbackConnection();
    }

    public LinkState State => _self == null ? LinkState.Offline : _connection.State;

    public string Status => _self == null ? "Waiting for character." : _connection.Status;

    public string? SelfId => _self?.Id;

    public IReadOnlyList<SessionSummary> Sessions => _connection.Sessions;

    public SessionSnapshot? Snapshot => _connection.Snapshot;

    public string? WatchedParticipantId { get; set; }

    public int ExpectedOwnDice { get; private set; }

    public bool UsingServer => _usingSocket;

    public string? Banner => DateTime.UtcNow - _bannerAt < TimeSpan.FromSeconds(6) ? _banner : null;

    public void Configure(string serverUrl, string pluginKey)
    {
        _pluginKey = pluginKey.Trim();
        string? url = WebSocketConnection.CanonicalUrl(serverUrl);
        bool socket = !string.IsNullOrEmpty(url);
        if (socket == _usingSocket && string.Equals(url, _activeUrl, StringComparison.Ordinal))
        {
            if (_self != null)
                _connection.Connect(_self, _pluginKey);
            return;
        }

        _activeUrl = url;
        _usingSocket = socket;
        _connection.Dispose();
        _connection = socket
            ? new WebSocketConnection(_log, url!)
            : new LoopbackConnection();
        if (_self != null)
            _connection.Connect(_self, _pluginKey);
    }

    public void EnsureIdentity(CharacterIdentity identity)
    {
        if (_self != null && _self.Id == identity.Id)
            return;

        _self = identity;
        _connection.Connect(identity, _pluginKey);
    }

    public void Tick(DateTime utcNow)
    {
        _connection.Tick(utcNow);
        Watch(Snapshot);
        if (_heldScope != null && utcNow >= _nextHoldRefresh)
        {
            _connection.Send(new ProtocolMessage { Type = MessageType.EditBegin, Scope = _heldScope });
            _nextHoldRefresh = utcNow.AddSeconds(5);
        }
    }

    public string? TakeNotice()
    {
        string? notice = _connection.TakeNotice();
        if (string.IsNullOrWhiteSpace(notice))
            return null;

        _banner = notice;
        _bannerAt = DateTime.UtcNow;
        return notice;
    }

    public void Hold(string? scope)
    {
        if (string.Equals(scope, _heldScope, StringComparison.Ordinal))
            return;

        if (_heldScope != null)
            _connection.Send(new ProtocolMessage { Type = MessageType.EditEnd, Scope = _heldScope });

        _heldScope = scope;
        if (scope == null)
            return;

        _connection.Send(new ProtocolMessage { Type = MessageType.EditBegin, Scope = scope });
        _nextHoldRefresh = DateTime.UtcNow.AddSeconds(5);
    }

    public void CreateSession(string name, string password) =>
        Send(new ProtocolMessage { Type = MessageType.SessionCreate, SessionName = name, Password = password });

    public void Join(string sessionId) =>
        Send(new ProtocolMessage { Type = MessageType.SessionJoin, SessionId = sessionId });

    public void Close(string sessionId, string password) =>
        Send(new ProtocolMessage { Type = MessageType.SessionClose, SessionId = sessionId, Password = password });

    public void Leave()
    {
        WatchedParticipantId = null;
        ExpectedOwnDice = 0;
        _left = true;
        Hold(null);
        Send(new ProtocolMessage { Type = MessageType.SessionLeave });
    }

    public bool ConsumeLeft()
    {
        bool left = _left;
        _left = false;
        return left;
    }

    public void ExpectOwnDice(int count) => ExpectedOwnDice = count;

    public void SetRole(StaffRole role) =>
        Send(new ProtocolMessage { Type = MessageType.RoleSet, Role = role });

    public void Register(string first, string last, string world, string discord, int threads, int level) =>
        Send(new ProtocolMessage
        {
            Type = MessageType.ParticipantRegister,
            FirstName = first,
            LastName = last,
            World = world,
            Discord = discord,
            Threads = threads,
            Level = level,
        });

    public void Remove(string participantId, string password) =>
        Send(new ProtocolMessage
        {
            Type = MessageType.ParticipantRemove,
            ParticipantId = participantId,
            Password = password,
        });

    public void UpdateLevel(string participantId, int level) =>
        Send(new ProtocolMessage
        {
            Type = MessageType.ParticipantLevel,
            ParticipantId = participantId,
            Level = level,
        });

    public void SetThreads(string participantId, int threads, long revision) =>
        Send(new ProtocolMessage
        {
            Type = MessageType.ThreadsSet,
            ParticipantId = participantId,
            Threads = threads,
            Revision = revision,
        });

    public void Complete(string participantId, long revision) =>
        Send(new ProtocolMessage
        {
            Type = MessageType.ParticipantComplete,
            ParticipantId = participantId,
            Revision = revision,
        });

    public void Pass(string participantId, long revision) =>
        Send(new ProtocolMessage
        {
            Type = MessageType.TrialPass,
            ParticipantId = participantId,
            Revision = revision,
        });

    public void Fail(string participantId, long revision) =>
        Send(new ProtocolMessage
        {
            Type = MessageType.TrialFail,
            ParticipantId = participantId,
            Revision = revision,
        });

    public void Say(string text)
    {
        string trimmed = text.Trim();
        if (trimmed.Length == 0)
            return;

        Send(new ProtocolMessage
        {
            Type = MessageType.LobbyChat,
            Text = trimmed,
        });
        Remember(LobbyKind.Chat, trimmed);
    }

    private void Watch(SessionSnapshot? snapshot)
    {
        if (snapshot == null)
            return;

        if (!string.Equals(_logSessionId, snapshot.Id, StringComparison.OrdinalIgnoreCase))
        {
            _logSessionId = snapshot.Id;
            _localLog.Clear();
            if (_config.LobbyLogs.TryGetValue(snapshot.Id, out List<LobbyLine>? saved))
            {
                foreach (LobbyLine line in saved)
                    _localLog.Add(line.Clone());
            }

            _watched = null;
        }

        foreach (LobbyLine line in snapshot.Log)
            Push(line.Name, line.Role, line.Kind, line.Text, line.At);

        if (_watched == null)
        {
            _watched = snapshot.Clone();
            return;
        }

        foreach (Participant player in snapshot.Participants)
        {
            Participant? previous = _watched.Participant(player.Id);
            if (previous == null)
            {
                Remember(snapshot, player.LastEditor, LobbyKind.Register, $"Registered {player.FullName} · {player.World} with {player.Threads} threads");
                continue;
            }

            if (previous.Threads != player.Threads)
            {
                SessionMember? editor = snapshot.Members.FirstOrDefault(member =>
                    string.Equals(member.Name, player.LastEditor, StringComparison.OrdinalIgnoreCase));
                bool failed = editor != null
                    && StaffText.IsGod(editor.Role)
                    && player.Threads == previous.Threads - 1;
                if (failed)
                {
                    string aspect = snapshot.Board(player.Id) is { } board
                        ? StaffText.AspectName(board.Aspect)
                        : "a trial";
                    Remember(snapshot, player.LastEditor, LobbyKind.Fail, $"{player.FullName} failed {aspect}. {previous.Threads} → {player.Threads} threads");
                }
                else
                {
                    Remember(snapshot, player.LastEditor, LobbyKind.Threads, $"{player.FullName}  {previous.Threads} → {player.Threads} threads");
                }
            }
            if (player.Level > previous.Level)
                Remember(snapshot, player.LastEditor, LobbyKind.Run, $"{player.FullName} started run {player.Level}");
            Aspect(snapshot, player, previous.Strength, player.Strength, "Strength");
            Aspect(snapshot, player, previous.Harmony, player.Harmony, "Harmony");
            Aspect(snapshot, player, previous.Fear, player.Fear, "Fear");
            Aspect(snapshot, player, previous.Power, player.Power, "Power");
        }

        foreach (Participant previous in _watched.Participants)
        {
            if (snapshot.Participant(previous.Id) == null)
                Remember(snapshot, previous.LastEditor, LobbyKind.Remove, $"Removed {previous.FullName}");
        }

        foreach (SessionMember member in snapshot.Members)
        {
            SessionMember? previous = _watched.Member(member.Id);
            if (previous == null)
                Remember(snapshot, member.Name, LobbyKind.Join, "Joined the lobby");
            else if (previous.Role != member.Role)
                Remember(snapshot, member.Name, LobbyKind.Role, $"Took the {StaffText.Label(member.Role)} seat");
        }

        foreach (SessionMember previous in _watched.Members)
        {
            if (snapshot.Member(previous.Id) == null)
                Remember(snapshot, previous.Name, LobbyKind.Leave, "Left the lobby");
        }

        _watched = snapshot.Clone();
    }

    private void Aspect(SessionSnapshot snapshot, Participant player, StaffRole? before, StaffRole? after, string trial)
    {
        if (before == after || after is null or StaffRole.None)
            return;
        Remember(snapshot, player.LastEditor, LobbyKind.Pass, $"{player.FullName} claimed Trial of {trial} · {StaffText.Label(after.Value)}");
    }

    private void Remember(LobbyKind kind, string text)
    {
        SessionSnapshot? snapshot = Snapshot;
        string name = snapshot?.Member(SelfId ?? "")?.Name ?? "You";
        StaffRole role = snapshot?.Member(SelfId ?? "")?.Role ?? StaffRole.None;
        Push(name, role, kind, text);
    }

    private void Remember(SessionSnapshot snapshot, string editor, LobbyKind kind, string text)
    {
        SessionMember? actor = snapshot.Members.FirstOrDefault(member =>
            string.Equals(member.Name, editor, StringComparison.OrdinalIgnoreCase));
        Push(actor?.Name ?? (string.IsNullOrWhiteSpace(editor) ? "Table" : editor), actor?.Role ?? StaffRole.None, kind, text);
    }

    private void Push(string name, StaffRole role, LobbyKind kind, string text)
    {
        Push(name, role, kind, text, DateTime.UtcNow);
    }

    private void Push(string name, StaffRole role, LobbyKind kind, string text, DateTime at)
    {
        if (_localLog.Any(line =>
                line.Kind == kind
                && line.Name == name
                && line.Text == text
                && Math.Abs((line.At - at).TotalSeconds) < 3))
            return;

        _localLog.Add(new LobbyLine
        {
            At = at,
            Name = name,
            Role = role,
            Kind = kind,
            Text = text,
        });
        _localLog.Sort((left, right) => left.At.CompareTo(right.At));
        if (_localLog.Count > 200)
            _localLog.RemoveRange(0, _localLog.Count - 200);
        if (_logSessionId == null)
            return;

        _config.LobbyLogs[_logSessionId] = _localLog.Select(line => line.Clone()).ToList();
        _config.Save();
    }

    public void ReportDie(string participantId, string side, int value)
    {
        SessionSnapshot? before = Snapshot;
        TrialBoard? board = before?.Board(participantId);
        var rolls = new List<int>(side == "god" ? board?.GodRolls ?? [] : board?.PlayerRolls ?? []);
        rolls.Add(value);
        long revision = board?.Revision ?? 0;
        Send(new ProtocolMessage
        {
            Type = MessageType.TrialDice,
            ParticipantId = participantId,
            Side = side,
            Values = rolls,
            Revision = revision,
        });

        if (!ReferenceEquals(Snapshot, before) || before == null)
            return;

        TrialBoard shown = before.Board(participantId) ?? new TrialBoard
        {
            ParticipantId = participantId,
        };
        if (!before.Boards.Contains(shown))
            before.Boards.Add(shown);
        if (side == "god") shown.GodRolls = rolls;
        else shown.PlayerRolls = rolls;
    }

    public void ClearDice(string participantId, string side, long revision)
    {
        Send(new ProtocolMessage
        {
            Type = MessageType.TrialDice,
            ParticipantId = participantId,
            Side = side,
            Values = [],
            Revision = revision,
        });

        TrialBoard? board = Snapshot?.Board(participantId);
        if (board == null || board.Revision != revision)
            return;
        if (side == "god")
            board.GodRolls = [];
        else
            board.PlayerRolls = [];
    }

    public bool LockedByOther(string scope, out string holder)
    {
        holder = "";
        EditLock? edit = Snapshot?.Lock(scope, DateTime.UtcNow);
        if (edit == null || string.Equals(edit.HolderId, SelfId, StringComparison.OrdinalIgnoreCase))
            return false;

        holder = edit.HolderName;
        return true;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private void Send(ProtocolMessage message) => _connection.Send(message);
}
