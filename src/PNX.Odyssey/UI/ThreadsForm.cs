using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Pnx.Odyssey.Core;
using Pnx.Odyssey.Services;

namespace Pnx.Odyssey.UI;

internal sealed class ThreadsForm
{
    private string _search = "";
    private string? _selectedId;
    private int _draft = TrialRules.StartingThreads;
    private string _followed = "";
    private string? _tradeNote;

    public string? Draw(Plugin plugin)
    {
        SessionSnapshot? snapshot = plugin.Client.Snapshot;
        if (snapshot == null)
            return null;

        FollowTarget(plugin, snapshot);
        Ui.Hint("Target a player, or search for them.");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##thread-search", ref _search, 64);
        if (_tradeNote != null)
            ImGui.TextColored(Ui.Amber, _tradeNote);

        Ui.Space();
        ImGui.BeginChild("thread-players", new Vector2(300, 280), true);
        foreach (Participant participant in Matching(snapshot))
        {
            if (ImGui.Selectable($"{participant.FullName}  ·  {participant.World}##{participant.Id}", participant.Id == _selectedId))
                Select(participant);
        }

        ImGui.EndChild();
        ImGui.SameLine(0, 16);
        DrawEditor(plugin, snapshot);

        Participant? selected = snapshot.Participant(_selectedId ?? "");
        return selected == null ? null : EditScope.Threads(selected.Id);
    }

    private void DrawEditor(Plugin plugin, SessionSnapshot snapshot)
    {
        Participant? selected = snapshot.Participant(_selectedId ?? "");
        Vector2 start = ImGui.GetCursorScreenPos();
        ImGui.BeginGroup();
        if (selected == null)
        {
            Ui.Hint("Choose a registered player, or target them.");
            ImGui.EndGroup();
            Frame(start, targeted: false);
            return;
        }

        bool locked = plugin.Client.LockedByOther(EditScope.Threads(selected.Id), out string holder);
        bool targeted = IsTargeted(plugin, selected);
        ImGui.SetWindowFontScale(1.45f);
        ImGui.Text(selected.FullName);
        ImGui.SetWindowFontScale(1f);
        Ui.Hint(selected.World);
        Ui.Space();
        if (selected.IsShade)
            ImGui.TextColored(Ui.Danger, "Shade");
        ImGui.AlignTextToFramePadding();
        Ui.ThreadMark(22f);
        ImGui.SameLine(0, 8);
        ImGui.Text($"Threads    {_draft}");
        ImGui.BeginDisabled(locked || _draft <= 0);
        if (ImGui.Button("-##threads", new Vector2(36, 32)))
            _draft--;
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(locked || _draft >= TrialRules.MaxThreads);
        if (ImGui.Button("+##threads", new Vector2(36, 32)))
            _draft++;
        ImGui.EndDisabled();

        Ui.Space();
        int cost = TrialRules.OfferingCost(selected.Threads, _draft);
        Ui.Hint($"Offering    {cost.ToString("N0", CultureInfo.InvariantCulture)} gil");
        if (locked)
            Ui.Editing(holder);

        Ui.Space();
        if (ImGui.Button("Open trade"))
        {
            if (plugin.TargetParticipant(selected))
                _tradeNote = ChatMacro.Run(["/trade"]) ? null : "Could not open trade.";
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(locked || _draft == selected.Threads);
        if (ImGui.Button("Apply", new Vector2(140, 32)))
            plugin.Client.SetThreads(selected.Id, _draft, selected.Revision);
        ImGui.EndDisabled();
        ImGui.EndGroup();
        Frame(start, targeted);
    }

    private static void Frame(Vector2 start, bool targeted)
    {
        Vector2 min = ImGui.GetItemRectMin();
        Vector2 max = ImGui.GetItemRectMax();
        if (max.X <= min.X)
            max = start + new Vector2(220, 36);
        Vector2 pad = new(8, 6);
        min -= pad;
        max += pad;
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        draw.AddRect(min, max, ImGui.GetColorU32(new Vector4(0.45f, 0.38f, 0.62f, 0.7f)), 6f);
        if (!targeted)
            return;

        float pulse = 0.45f + (0.55f * MathF.Sin((float)ImGui.GetTime() * 5f));
        uint bracket = ImGui.GetColorU32(new Vector4(0.98f, 0.82f, 0.28f, pulse));
        const float arm = 14f;
        const float thick = 2.2f;
        Corner(draw, min, new Vector2(arm, arm), bracket, thick);
        Corner(draw, new Vector2(max.X, min.Y), new Vector2(-arm, arm), bracket, thick);
        Corner(draw, new Vector2(min.X, max.Y), new Vector2(arm, -arm), bracket, thick);
        Corner(draw, max, new Vector2(-arm, -arm), bracket, thick);
    }

    private static void Corner(ImDrawListPtr draw, Vector2 origin, Vector2 arm, uint color, float thick)
    {
        draw.AddLine(origin, origin + new Vector2(arm.X, 0), color, thick);
        draw.AddLine(origin, origin + new Vector2(0, arm.Y), color, thick);
    }

    private static bool IsTargeted(Plugin plugin, Participant selected)
    {
        if (!plugin.TryTarget(out PartyPresence target))
            return false;
        return PartyMatcher.SamePerson(target.Name, target.World, selected);
    }

    private void FollowTarget(Plugin plugin, SessionSnapshot snapshot)
    {
        if (!plugin.TryTarget(out PartyPresence target))
            return;

        string key = $"{target.Name}@{target.World}";
        if (key == _followed)
            return;

        _followed = key;
        Participant? match = PartyMatcher.Find(snapshot, target.Name, target.World);
        if (match != null)
            Select(match);
    }

    private void Select(Participant participant)
    {
        if (_selectedId == participant.Id)
            return;
        _selectedId = participant.Id;
        _draft = participant.Threads;
    }

    private IEnumerable<Participant> Matching(SessionSnapshot snapshot)
    {
        foreach (Participant participant in snapshot.Participants)
        {
            if (_search.Length == 0
                || participant.FullName.Contains(_search, StringComparison.OrdinalIgnoreCase)
                || participant.World.Contains(_search, StringComparison.OrdinalIgnoreCase))
                yield return participant;
        }
    }
}
