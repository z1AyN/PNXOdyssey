namespace Pnx.Odyssey.Core;

public sealed class GodMacro
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Text { get; set; } = "";

    public string AuthorId { get; set; } = "";

    public string AuthorName { get; set; } = "";

    public GodMacro Clone() => new()
    {
        Id = Id,
        Name = Name,
        Text = Text,
        AuthorId = AuthorId,
        AuthorName = AuthorName,
    };
}
