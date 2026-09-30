using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pnx.Odyssey.Core;

public enum LinkState
{
    Offline,
    Syncing,
    Connected,
}

public static class MessageType
{
    public const string Hello = "hello";
    public const string Heartbeat = "heartbeat";
    public const string SessionCreate = "session.create";
    public const string SessionJoin = "session.join";
    public const string SessionClose = "session.close";
    public const string SessionLeave = "session.leave";
    public const string RoleSet = "role.set";
    public const string ParticipantRegister = "participant.register";
    public const string ParticipantRemove = "participant.remove";
    public const string ParticipantLevel = "participant.level";
    public const string ParticipantComplete = "participant.complete";
    public const string ThreadsSet = "threads.set";
    public const string EditBegin = "edit.begin";
    public const string EditEnd = "edit.end";
    public const string TrialDice = "trial.dice";
    public const string TrialFail = "trial.fail";
    public const string TrialPass = "trial.pass";
    public const string LobbyChat = "lobby.chat";
    public const string Welcome = "welcome";
    public const string State = "state";
    public const string Sessions = "sessions";
    public const string Rejected = "rejected";
}

public sealed class ProtocolMessage
{
    public string Type { get; set; } = "";

    public string? Name { get; set; }

    public string? World { get; set; }

    public string? PluginKey { get; set; }

    public string? SessionId { get; set; }

    public string? SessionName { get; set; }

    public string? Password { get; set; }

    public StaffRole? Role { get; set; }

    public string? ParticipantId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Discord { get; set; }

    public int? Threads { get; set; }

    public int? Level { get; set; }

    public long? Revision { get; set; }

    public string? Scope { get; set; }

    public string? Side { get; set; }

    public List<int>? Values { get; set; }

    public string? Text { get; set; }

    public string? Reason { get; set; }

    public string? SavedBy { get; set; }

    public string? SelfId { get; set; }

    public List<SessionSummary>? Sessions { get; set; }

    public SessionSnapshot? Session { get; set; }
}

public static class ProtocolJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}

public interface IOdysseyConnection : IDisposable
{
    LinkState State { get; }

    string Status { get; }

    string? SelfId { get; }

    IReadOnlyList<SessionSummary> Sessions { get; }

    SessionSnapshot? Snapshot { get; }

    void Connect(CharacterIdentity self, string? pluginKey);

    void Disconnect();

    void Send(ProtocolMessage message);

    void Tick(DateTime utcNow);

    string? TakeNotice();
}
