using Dalamud.Configuration;
using Dalamud.Plugin;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey;

public sealed class Configuration : IPluginConfiguration
{
    public const string ServerAddress = "wss://odyssey.pnx.events/odyssey";

    public const string PluginKeyValue = "080396d5144461497dee4e6a0c97799561ffaa49fa84489b";

    public int Version { get; set; } = 1;

    public string ServerUrl { get; set; } = "";

    public string PluginKey { get; set; } = "";

    public string GameStartMacro { get; set; } =
        "/p ──── Trial begins ────\n/p <t>, the gods are watching.";

    public string LastSessionId { get; set; } = "";

    public float OverlayHeight { get; set; } = 2.35f;

    public float OverlayOffset { get; set; } = 0.2f;

    public bool OverlayClamp { get; set; }

    public float OverlayScale { get; set; } = 1f;

    public float OverlayOpacity { get; set; } = 0.92f;

    public bool OverlayEnabled { get; set; } = true;

    public bool OverlayFade { get; set; }

    public float OverlayFadeSeconds { get; set; } = 6f;

    public int ChatSound { get; set; } = 1;

    public int ChatTextSize { get; set; } = 16;

    public bool ChatFollow { get; set; } = true;

    public int TableTextSize { get; set; } = 16;

    public bool EventMacrosReady { get; set; }

    public List<DjEntry> Djs { get; set; } = [];

    public List<ShoutMacro> Macros { get; set; } = [];

    public List<string> GodHotbar { get; set; } = [];

    public Dictionary<string, List<LobbyLine>> LobbyLogs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [NonSerialized]
    private IDalamudPluginInterface? _plugin;

    public void Initialize(IDalamudPluginInterface plugin)
    {
        _plugin = plugin;
        if (OverlayHeight < 0.2f)
            OverlayHeight = 2.35f;
        if (OverlayScale < 0.4f)
            OverlayScale = 1f;
        if (OverlayOpacity <= 0f || OverlayOpacity > 1f)
            OverlayOpacity = 0.92f;
        if (OverlayFadeSeconds < 1f || OverlayFadeSeconds > 60f)
            OverlayFadeSeconds = 6f;
        if (ChatSound < 1 || ChatSound > 16)
            ChatSound = 1;
        if (ChatTextSize < 12 || ChatTextSize > 24)
            ChatTextSize = 16;
        if (TableTextSize < 12 || TableTextSize > 24)
            TableTextSize = 16;
        LobbyLogs ??= new Dictionary<string, List<LobbyLine>>(StringComparer.OrdinalIgnoreCase);
        Djs ??= [];
        Macros ??= [];
        GodHotbar ??= [];
        if (!EventMacrosReady)
        {
            Djs = EventDefaults.Djs();
            Macros = EventDefaults.Macros();
            EventMacrosReady = true;
            Save();
        }

        bool touched = false;
        foreach (DjEntry dj in Djs)
        {
            string shortLink = EventDefaults.ShortTwitch(dj.Twitch);
            if (shortLink != dj.Twitch)
            {
                dj.Twitch = shortLink;
                touched = true;
            }
        }

        foreach (ShoutMacro macro in Macros)
        {
            if (macro.ChannelChosen || macro.Dj)
                continue;
            macro.Yell = macro.Text.Contains("/y ", StringComparison.Ordinal) || macro.Text.Contains("/yell ", StringComparison.OrdinalIgnoreCase);
            macro.ChannelChosen = true;
            touched = true;
        }

        if (touched)
            Save();
    }

    public void Save() => _plugin?.SavePluginConfig(this);

    public string[] GameStartLines() => GameStartMacro
        .Replace("\r", "", StringComparison.Ordinal)
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
