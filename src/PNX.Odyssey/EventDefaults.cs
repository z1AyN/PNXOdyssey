namespace Pnx.Odyssey;

public sealed class DjEntry
{
    public string Name { get; set; } = "";

    public string Twitch { get; set; } = "";

    public long StartUnix { get; set; }
}

public sealed class ShoutMacro
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Text { get; set; } = "";

    public bool Dj { get; set; }

    public bool Yell { get; set; }

    public bool ChannelChosen { get; set; }

    public string Prefix { get; set; } = "";

    public int IntervalMinutes { get; set; }

    public long LastPushedUnix { get; set; }

    public long NextPushUnix { get; set; }
}

public static class EventDefaults
{
    public static List<DjEntry> Djs() =>
    [
        Dj("Frosty Cupcake", "twitch.tv/frosty_cupcake", 1791046800),
        Dj("Kiwi", "twitch.tv/kiwinjoyer", 1791050400),
        Dj("Khangomon", "twitch.tv/khangomon", 1791054000),
        Dj("Raindrop Kitty", "twitch.tv/raindropkitty", 1791057600),
        Dj("Aemilia", "twitch.tv/aemilia", 1791061200),
        Dj("Elana", "twitch.tv/elanaastaria", 1791064800),
        Dj("Cynaxia", "twitch.tv/cynaxia", 1791068400),
        Dj("Rey Alex", "twitch.tv/reyalexxx", 1791072000),
        Dj("Swage", "twitch.tv/swage", 1791075600),
    ];

    public static List<ShoutMacro> Macros() =>
    [
        new ShoutMacro
        {
            Id = "discord",
            Name = "Discord/Website",
            Text = """
                /sh Lost on your Odyssey? Event highlights, immersive photography, and the full experience await on our Discord and website:
                /wait 2
                /sh → https://discord.gg/pnx | pnx.events/odyssey • Gamble with fate. Rise through Olympus.
                """,
        },
        new ShoutMacro
        {
            Id = "threads",
            Name = "Threads",
            Text = "/y No mortal challenges fate empty-handed. Gather your threads of fate from Furia, Soda or Masha and prepare to wager them in the Trials of Olympus. pnx.events/odyssey/trials",
        },
        new ShoutMacro
        {
            Id = "glam",
            Name = "Glam Contest",
            Text = """
                /y Don your finest armour, divine silks or mythical attire and step before the Council. Show Olympus a glamour worthy of legend and prove that even the gods can be outshone.
                /wait 2
                /y Join the Glam Contest on our Discord for a chance to win 5M gil! https://discord.gg/pnx
                """,
        },
        new ShoutMacro
        {
            Id = "dj",
            Name = "DJ",
            Dj = true,
        },
    ];

    public static IReadOnlyList<string> Lines(ShoutMacro macro, IReadOnlyList<DjEntry> djs, long now)
    {
        if (!macro.Dj)
        {
            return macro.Text
                .Replace("\r", "", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => Style(line, macro))
                .ToList();
        }

        List<DjEntry> ordered = djs.OrderBy(entry => entry.StartUnix).ToList();
        DjSchedule.Pick pick = DjSchedule.Choose(ordered.ConvertAll(entry => entry.StartUnix), now);
        string? playing = pick.Current >= 0 ? ordered[pick.Current].Name : null;
        string? upcoming = pick.Next >= 0 ? ordered[pick.Next].Name : null;
        string? link = pick.Current >= 0
            ? ShortTwitch(ordered[pick.Current].Twitch)
            : pick.Next >= 0 ? ShortTwitch(ordered[pick.Next].Twitch) : null;
        string body = DjSchedule.Shout(playing, link, upcoming);
        return body.Length == 0 ? [] : [Style(body, macro)];
    }

    public static string Style(string line, ShoutMacro macro)
    {
        string trimmed = line.Trim();
        if (trimmed.StartsWith("/wait", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        string body = StripCommand(trimmed);
        if (!string.IsNullOrEmpty(macro.Prefix) && !body.StartsWith(macro.Prefix, StringComparison.Ordinal))
            body = $"{macro.Prefix} {body}";
        return $"{(macro.Yell ? "/y" : "/sh")} {body}".Trim();
    }

    public static string ShortTwitch(string link)
    {
        string value = link.Trim();
        string[] prefixes = ["https://www.", "http://www.", "https://", "http://", "www."];
        foreach (string prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..];
                break;
            }
        }

        return value;
    }

    private static string StripCommand(string line)
    {
        string[] commands = ["/shout", "/yell", "/sh", "/y"];
        foreach (string command in commands)
        {
            if (!line.StartsWith(command, StringComparison.OrdinalIgnoreCase))
                continue;
            if (line.Length == command.Length)
                return "";
            if (line[command.Length] == ' ')
                return line[(command.Length + 1)..].TrimStart();
        }

        return line;
    }

    public static bool BuiltIn(string id) => id is "discord" or "threads" or "glam" or "dj";

    private static DjEntry Dj(string name, string twitch, long start) => new()
    {
        Name = name,
        Twitch = twitch,
        StartUnix = start,
    };
}
