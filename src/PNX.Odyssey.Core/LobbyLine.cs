namespace Pnx.Odyssey.Core;

public enum LobbyKind
{
    Chat,
    Join,
    Leave,
    Role,
    Register,
    Remove,
    Threads,
    Run,
    Pass,
    Fail,
}

public sealed class LobbyLine
{
    public DateTime At { get; set; }

    public string Name { get; set; } = "";

    public StaffRole Role { get; set; }

    public LobbyKind Kind { get; set; }

    public string Text { get; set; } = "";

    public LobbyLine Clone() => new()
    {
        At = At,
        Name = Name,
        Role = Role,
        Kind = Kind,
        Text = Text,
    };
}
