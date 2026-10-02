using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Pnx.Odyssey.Core;
using Pnx.Odyssey.Services;

namespace Pnx.Odyssey.UI;

internal sealed class MainWindow : Window
{
    private readonly Plugin _plugin;
    private readonly SessionForm _sessions = new();
    private readonly RegisterForm _register = new();
    private readonly ThreadsForm _threads = new();
    private readonly GodForm _god = new();
    private readonly AspectsForm _aspects = new();
    private readonly MacrosForm _macros = new();
    private readonly WatcherForm _watcher = new();
    private string? _hostNote;

    public MainWindow(Plugin plugin) : base("PNX Odyssey##main", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _plugin = plugin;
        Size = new Vector2(980, 640);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(820, 480),
            MaximumSize = new Vector2(1400, 980),
        };
    }

    public override void PreDraw() => Ui.PushFrame();

    public override void PostDraw() => Ui.PopFrame();

    public override void Draw()
    {
        float footer = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y + 20f;
        float height = Math.Max(80f, ImGui.GetContentRegionAvail().Y - footer);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0f));
        bool body = ImGui.BeginChild(
            "main-body",
            new Vector2(0, height),
            false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleColor();
        if (body)
        {
            DrawContents();
            ImGui.EndChild();
        }

        SessionChrome.Footer(_plugin);
    }

    private void DrawContents()
    {
        string? scope = null;
        SessionSnapshot? snapshot = _plugin.Client.Snapshot;
        if (snapshot == null)
        {
            _sessions.Draw(_plugin);
        }
        else
        {
            SessionChrome.Header(_plugin, snapshot);
            scope = DrawBody(snapshot);
        }

        _plugin.Client.Hold(scope);
        if (scope == null || !scope.StartsWith("trial:", StringComparison.Ordinal))
            _plugin.Client.WatchedParticipantId = null;
    }

    private string? DrawBody(SessionSnapshot snapshot)
    {
        StaffRole role = SessionChrome.RoleOf(_plugin.Client, snapshot);
        if (role == StaffRole.None)
        {
            if (!ImGui.BeginTabBar("seat"))
                return null;
            if (ImGui.BeginTabItem("Lobby"))
            {
                Ui.Hint("Pick a seat. Fate and the Director run the table. A god runs their own trial.");
                SessionChrome.Members(snapshot);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Watcher"))
            {
                _watcher.Draw(_plugin);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
            return null;
        }

        if (role is StaffRole.Fate or StaffRole.Director)
        {
            SessionChrome.Strip(snapshot);
            DrawHostBar(snapshot);
            return DrawHostTabs(role);
        }

        SessionChrome.Strip(snapshot);
        return DrawGodTabs(snapshot, role);
    }

    private void DrawHostBar(SessionSnapshot snapshot)
    {
        ImGui.AlignTextToFramePadding();
        if (ImGui.Button("Claims"))
            TellClaims(snapshot);
        ImGui.SameLine();
        foreach (string id in _plugin.Config.HostHotbar.ToList())
        {
            GodMacro? macro = snapshot.Macros.FirstOrDefault(item => item.Id == id);
            if (macro == null)
                continue;
            if (ImGui.Button($"{macro.Name}##host{id}"))
                _hostNote = RunLines(macro.Text) ? null : $"Could not run {macro.Name}.";
            ImGui.SameLine();
        }

        float libraryWidth = ImGui.CalcTextSize("Shared Library").X + 24f;
        float right = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - libraryWidth;
        if (right > ImGui.GetCursorPosX())
            ImGui.SameLine(right);
        if (ImGui.Button("Shared Library##host"))
            _plugin.OpenLibrary();
        if (_hostNote != null)
            ImGui.TextColored(Ui.Amber, _hostNote);
    }

    private void TellClaims(SessionSnapshot snapshot)
    {
        if (!_plugin.TryTarget(out PartyPresence target) || string.IsNullOrWhiteSpace(target.World))
        {
            _hostNote = "Target a player to tell them the remaining claims.";
            return;
        }

        IReadOnlyList<string> parts = AspectCatalog.RemainingSummary(snapshot.Claims);
        if (parts.Count == 0)
        {
            _hostNote = "No claims are left.";
            return;
        }

        _hostNote = ChatMacro.Run(PackTells($"{target.Name}@{target.World}", parts))
            ? null
            : "Could not send the claims tell.";
    }

    private static List<string> PackTells(string target, IReadOnlyList<string> parts)
    {
        string prefix = $"/tell {target} ";
        var lines = new List<string>();
        var current = new StringBuilder();
        foreach (string part in parts)
        {
            string next = current.Length == 0 ? prefix + part : current + "  " + part;
            if (Encoding.UTF8.GetByteCount(next) <= 180)
            {
                current.Clear();
                current.Append(next);
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current.ToString());
                lines.Add("/wait 2");
                current.Clear();
            }

            current.Append(prefix).Append(part);
        }

        if (current.Length > 0)
            lines.Add(current.ToString());
        return lines;
    }

    private static bool RunLines(string text)
    {
        string[] lines = text
            .Replace("\r", "", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ChatMacro.Run(lines);
    }

    private string? DrawHostTabs(StaffRole role)
    {
        string? scope = null;
        if (!ImGui.BeginTabBar("fate"))
            return null;

        if (ImGui.BeginTabItem("Register"))
        {
            scope = _register.Draw(_plugin);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Threads"))
        {
            scope = _threads.Draw(_plugin);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Players"))
        {
            scope = DrawPlayers();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Aspects"))
        {
            scope = DrawAspects();
            ImGui.EndTabItem();
        }

        if (role == StaffRole.Director && ImGui.BeginTabItem("Macros"))
        {
            _macros.Draw(_plugin);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Watcher"))
        {
            _watcher.Draw(_plugin);
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
        return scope;
    }

    private string? DrawGodTabs(SessionSnapshot snapshot, StaffRole role)
    {
        string? scope = null;
        if (!ImGui.BeginTabBar("god"))
            return null;

        if (ImGui.BeginTabItem("Game"))
        {
            scope = _god.Draw(_plugin, snapshot, role, GodTab.Game);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Players"))
        {
            _god.Draw(_plugin, snapshot, role, GodTab.Players);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Watcher"))
        {
            _watcher.Draw(_plugin);
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
        return scope;
    }

    private string? DrawPlayers()
    {
        PlayersTable.Draw(_plugin, _plugin.Client.Snapshot!, fate: true);
        return null;
    }

    private string? DrawAspects()
    {
        _aspects.Draw(_plugin, _plugin.Client.Snapshot!);
        return null;
    }
}
