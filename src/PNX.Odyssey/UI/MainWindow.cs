using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.UI;

internal sealed class MainWindow : Window
{
    private readonly Plugin _plugin;
    private readonly SessionForm _sessions = new();
    private readonly RegisterForm _register = new();
    private readonly ThreadsForm _threads = new();
    private readonly GodForm _god = new();

    public MainWindow(Plugin plugin) : base("PNX Odyssey##main")
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
        float footer = ImGui.GetFrameHeight() + 12f;
        float height = Math.Max(80f, ImGui.GetContentRegionAvail().Y - footer);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0f));
        bool body = ImGui.BeginChild("main-body", new Vector2(0, height));
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
            Ui.Banner(_plugin.Client.Banner, Ui.Amber);
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
            Ui.Hint("Pick a seat. Fate registers players. A god runs their own trial.");
            SessionChrome.Members(snapshot);
            return null;
        }

        if (role == StaffRole.Fate)
        {
            SessionChrome.Strip(snapshot);
            return DrawFateTabs();
        }

        SessionChrome.Strip(snapshot);
        return DrawGodTabs(snapshot, role);
    }

    private string? DrawFateTabs()
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

        ImGui.EndTabBar();
        return scope;
    }

    private string? DrawPlayers()
    {
        PlayersTable.Draw(_plugin, _plugin.Client.Snapshot!, fate: true);
        return null;
    }

    private static string? DrawAspects()
    {
        Ui.Section("Aspects");
        ImGui.TextWrapped("Claims will be listed here later. Aspect prizes from the Odyssey site are not loaded in this version.");
        return null;
    }
}
