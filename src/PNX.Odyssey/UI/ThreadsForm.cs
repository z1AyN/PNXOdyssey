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

        float listHeight = Math.Max(160f, ImGui.GetContentRegionAvail().Y);
        ImGui.BeginChild("thread-players", new Vector2(300, listHeight), true);
        foreach (Participant participant in Matching(snapshot))
        {
            if (ImGui.Selectable($"{participant.FullName}  ·  {participant.World}##{participant.Id}", participant.Id == _selectedId))
                Select(participant);
        }

        ImGui.EndChild();
        ImGui.SameLine(0, 16);
        ImGui.BeginChild("thread-stage", new Vector2(0, listHeight));
        DrawEditor(plugin, snapshot);
        ImGui.EndChild();

        Participant? selected = snapshot.Participant(_selectedId ?? "");
        return selected == null ? null : EditScope.Threads(selected.Id);
    }

    private Vector2 _card = new(280, 220);

    private void DrawEditor(Plugin plugin, SessionSnapshot snapshot)
    {
        Participant? selected = snapshot.Participant(_selectedId ?? "");
        Vector2 avail = ImGui.GetContentRegionAvail();
        Vector2 origin = ImGui.GetCursorPos();
        float x = origin.X + Math.Max(0f, (avail.X - _card.X) * 0.5f);
        float y = origin.Y + Math.Max(0f, (avail.Y - _card.Y) * 0.5f);
        ImGui.SetCursorPos(new Vector2(x, y));
        Vector2 stageMin = ImGui.GetWindowPos();
        Vector2 stageMax = stageMin + ImGui.GetWindowSize();
        Vector2 plateMin = ImGui.GetCursorScreenPos();
        Vector2 plateMax = plateMin + _card;
        Pulse(stageMin, stageMax, plateMin, plateMax);
        ImDrawListPtr plate = ImGui.GetWindowDrawList();
        plate.AddRectFilled(plateMin, plateMax, ImGui.GetColorU32(new Vector4(0.055f, 0.043f, 0.086f, 1f)), 6f);
        ImGui.BeginGroup();
        if (selected == null)
        {
            Ui.Hint("Choose a registered player, or target them.");
            ImGui.EndGroup();
            RememberCard();
            Frame(ImGui.GetItemRectMin(), targeted: false);
            return;
        }

        bool locked = plugin.Client.LockedByOther(EditScope.Threads(selected.Id), out string holder);
        bool targeted = IsTargeted(plugin, selected);
        ImGui.Text(selected.FullName);
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
        Ui.Hint($"Offering    {Gil(cost)} gil");
        if (locked)
            Ui.Editing(holder);

        Ui.Space();
        if (ImGui.Button("Tell threads"))
            _tradeNote = ChatMacro.Run([ThreadTell(selected)]) ? null : "Could not tell them their threads.";
        ImGui.SameLine();
        if (ImGui.Button("Open trade"))
        {
            plugin.TargetParticipant(selected);
            string pay = PayTell(selected, _draft, cost);
            _tradeNote = ChatMacro.Run([pay, "/wait 1", "/trade"]) ? null : "Could not open trade.";
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(locked || _draft == selected.Threads);
        if (ImGui.Button("Apply", new Vector2(140, 32)))
            plugin.Client.SetThreads(selected.Id, _draft, selected.Revision);
        ImGui.EndDisabled();
        ImGui.EndGroup();
        RememberCard();
        Frame(ImGui.GetItemRectMin(), targeted);
    }

    private void RememberCard()
    {
        Vector2 size = ImGui.GetItemRectSize() + new Vector2(16, 12);
        if (MathF.Abs(size.X - _card.X) < 4f && MathF.Abs(size.Y - _card.Y) < 4f)
            return;
        if (size.X > 8 && size.Y > 8)
            _card = new Vector2(MathF.Ceiling(size.X), MathF.Ceiling(size.Y));
    }

    private static void Frame(Vector2 start, bool targeted)
    {
        Vector2 min = start;
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

    private static void Pulse(Vector2 areaMin, Vector2 areaMax, Vector2 cardMin, Vector2 cardMax)
    {
        if (cardMax.X <= cardMin.X || cardMax.Y <= cardMin.Y)
            return;

        float wave = 0.55f + (0.45f * MathF.Sin((float)ImGui.GetTime() * 2.2f));
        uint ink = ImGui.GetColorU32(new Vector4(0.68f, 0.54f, 0.96f, 0.25f + (0.45f * wave)));
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        const float gap = 10f;
        Vector2 left = new(cardMin.X - gap, (cardMin.Y + cardMax.Y) * 0.5f);
        Vector2 right = new(cardMax.X + gap, left.Y);
        Vector2 top = new((cardMin.X + cardMax.X) * 0.5f, cardMin.Y - gap);
        Vector2 bottom = new(top.X, cardMax.Y + gap);
        draw.AddLine(new Vector2(areaMin.X + 6, left.Y), left, ink, 1.5f);
        draw.AddLine(new Vector2(areaMax.X - 6, right.Y), right, ink, 1.5f);
        draw.AddLine(new Vector2(top.X, areaMin.Y + 6), top, ink, 1.5f);
        draw.AddLine(new Vector2(bottom.X, areaMax.Y - 6), bottom, ink, 1.5f);
    }

    private static string ThreadTell(Participant selected) =>
        $"/tell {selected.FullName}@{selected.World} You have {selected.Threads} threads left. One thread costs {Gil(TrialRules.GilPerThread)} gil.";

    private static string PayTell(Participant selected, int draft, int cost)
    {
        string who = $"{selected.FullName}@{selected.World}";
        int extra = Math.Max(0, draft - selected.Threads);
        return extra == 0
            ? $"/tell {who} No gil is due."
            : $"/tell {who} Please offer {Gil(cost)} gil for {extra} thread{(extra == 1 ? "" : "s")}.";
    }

    private static string Gil(int amount) => amount.ToString("N0", CultureInfo.InvariantCulture);

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
