using System.Numerics;
using Dalamud.Bindings.ImGui;
using Pnx.Odyssey.Core;
using Pnx.Odyssey.Services;

namespace Pnx.Odyssey.UI;

internal enum GodTab
{
    Game,
    Players,
}

internal sealed class GodForm
{
    private string? _selectedId;
    private string? _macroNote;
    private float _progress;

    public string? Draw(Plugin plugin, SessionSnapshot snapshot, StaffRole role, GodTab tab)
    {
        if (tab == GodTab.Players)
        {
            PlayersTable.Draw(plugin, snapshot, fate: false);
            return null;
        }

        DrawQuickActions(plugin, snapshot);
        DrawMacroBar(plugin, snapshot);
        if (_macroNote != null)
            ImGui.TextColored(Ui.Amber, _macroNote);

        Participant? challenger = snapshot.Participant(_selectedId ?? "");
        TrialAspect? aspect = StaffText.AspectOf(role);
        if (challenger == null || aspect == null)
            return null;

        plugin.Client.WatchedParticipantId = challenger.Id;
        bool locked = plugin.Client.LockedByOther(EditScope.Trial(challenger.Id), out string holder);
        TrialBoard? board = snapshot.Board(challenger.Id);
        IReadOnlyList<int> playerRolls = board?.PlayerRolls ?? [];
        IReadOnlyList<int> godRolls = board?.GodRolls ?? [];
        bool open = TrialRules.CanRoll(challenger, aspect.Value);
        bool already = TrialRules.Victor(challenger, aspect.Value) != null;
        if (locked)
            Ui.Editing(holder);

        DrawVerdict(plugin, challenger, open, locked, already);
        DrawStatus(challenger);
        long boardRevision = board?.Revision ?? 0;
        Vector2 tableOrigin = ImGui.GetCursorScreenPos();
        float tableWidth = Math.Max(1f, ImGui.GetContentRegionAvail().X);
        Ui.DiceTable("Player", playerRolls, StaffText.Label(role), godRolls, () =>
        {
            DrawReset(plugin, challenger, "player", boardRevision, locked || playerRolls.Count == 0);
        }, () =>
        {
            ImGui.BeginDisabled(!open || locked);
            if (ImGui.Button("Roll"))
            {
                plugin.Client.ExpectOwnDice(3);
                _macroNote = ChatMacro.Run(TrialRules.GodRollMacro(role)) ? null : "Could not run the dice macro.";
                if (_macroNote != null)
                    plugin.Client.ExpectOwnDice(0);
            }
            ImGui.EndDisabled();
            ImGui.SameLine();
            DrawReset(plugin, challenger, "god", boardRevision, locked || godRolls.Count == 0);
        });
        if (already)
        {
            Vector2 tableMax = ImGui.GetItemRectMax();
            CoverPassed(challenger, tableOrigin, new Vector2(tableOrigin.X + tableWidth, tableMax.Y));
        }

        DrawCondition(role, aspect.Value, playerRolls, godRolls, already);
        if (!open)
            Ui.Hint(challenger.Threads < 1
                ? "No threads remain. A Fate can restore them."
                : "Zeus unlocks after Strength, Harmony, and Fear.");
        return EditScope.Trial(challenger.Id);
    }

    private static void DrawCondition(
        StaffRole role,
        TrialAspect aspect,
        IReadOnlyList<int> playerRolls,
        IReadOnlyList<int> godRolls,
        bool already)
    {
        string rule = aspect switch
        {
            TrialAspect.Harmony => "Closest to 11 wins. Both sides roll 3d6.",
            TrialAspect.Fear => "More odd faces wins. Both sides roll 3d6.",
            TrialAspect.Power => "Higher total wins. The player rolls 3d6. Zeus rolls 3d8.",
            _ => $"Higher total wins. Both sides roll 3d{TrialRules.DieSides(role)}.",
        };
        ImGui.Text(rule);

        SuggestedCall call = TrialRules.Suggest(aspect, playerRolls, godRolls);
        if (already || call == SuggestedCall.Pass)
            ImGui.TextColored(Ui.Good, "Succeeded");
        else if (call == SuggestedCall.Fail)
            ImGui.TextColored(Ui.Danger, "Did not succeed");
        else if (call == SuggestedCall.Tie)
            ImGui.TextColored(Ui.Amber, "Tie");
        else
            Ui.Hint("Waiting on both sets of three dice.");

        RollSet player = DiceGrouping.Latest(playerRolls);
        RollSet god = DiceGrouping.Latest(godRolls);
        if (!player.IsComplete || !god.IsComplete)
            return;

        string figures = aspect switch
        {
            TrialAspect.Harmony => $"Player {Math.Abs(player.Total - TrialRules.HarmonyTarget)} from 11    {StaffText.Label(role)} {Math.Abs(god.Total - TrialRules.HarmonyTarget)} from 11",
            TrialAspect.Fear => $"Player {player.OddCount} odd    {StaffText.Label(role)} {god.OddCount} odd",
            _ => $"Player {player.Total}    {StaffText.Label(role)} {god.Total}",
        };
        Ui.Hint(figures);
    }

    private static void DrawVerdict(Plugin plugin, Participant challenger, bool open, bool locked, bool already)
    {
        if (!ImGui.BeginTable("verdict", 3, ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("fail", ImGuiTableColumnFlags.WidthFixed, 128);
        ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("pass", ImGuiTableColumnFlags.WidthFixed, 128);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.BeginDisabled(!open || locked);
        if (MarkButton("Fail", new Vector4(0.62f, 0.16f, 0.16f, 1f), new Vector4(0.98f, 0.94f, 0.92f, 1f), cross: true))
            plugin.Client.Fail(challenger.Id, challenger.Revision);
        ImGui.EndDisabled();
        ImGui.TableNextColumn();
        Ui.NameLine(challenger.FullName);
        ImGui.TableNextColumn();
        ImGui.BeginDisabled(!open || locked || already);
        if (MarkButton("Pass", new Vector4(0.20f, 0.55f, 0.28f, 1f), new Vector4(0.96f, 0.98f, 0.94f, 1f), cross: false))
            plugin.Client.Pass(challenger.Id, challenger.Revision);
        ImGui.EndDisabled();
        ImGui.EndTable();
    }

    private static void CoverPassed(Participant challenger, Vector2 min, Vector2 max)
    {
        if (max.X <= min.X || max.Y <= min.Y)
            return;

        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.02f, 0.02f, 0.04f, 0.72f)));
        string message = $"{challenger.FullName} has passed this Trial";
        Vector2 size = ImGui.CalcTextSize(message);
        Vector2 pos = min + ((max - min - size) * 0.5f);
        draw.AddText(pos, ImGui.GetColorU32(Ui.Good), message);
    }

    private static bool MarkButton(string label, Vector4 fill, Vector4 ink, bool cross)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, fill);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, fill with { W = 1f, X = Math.Min(1f, fill.X + 0.08f), Y = Math.Min(1f, fill.Y + 0.08f), Z = Math.Min(1f, fill.Z + 0.08f) });
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, fill);
        ImGui.PushStyleColor(ImGuiCol.Text, ink);
        bool pressed = ImGui.Button($"{(cross ? "×" : "✓")}  {label}", new Vector2(118, 36));
        ImGui.PopStyleColor(4);
        return pressed;
    }

    private static void DrawReset(Plugin plugin, Participant challenger, string side, long revision, bool disabled)
    {
        ImGui.BeginDisabled(disabled);
        if (ImGui.Button($"Reset##{side}"))
            plugin.Client.ClearDice(challenger.Id, side, revision);
        ImGui.EndDisabled();
    }

    private void DrawQuickActions(Plugin plugin, SessionSnapshot snapshot)
    {
        if (_selectedId == null)
            _selectedId = PartyMatcher.SingleChallenger(snapshot, plugin.ReadParty())?.Id;

        bool targeted = plugin.TryTarget(out _);
        ImGui.BeginDisabled(!targeted);
        if (ImGui.Button("Invite target"))
            _macroNote = ChatMacro.Run(["/invite"]) ? null : "Could not invite the target.";
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!targeted);
        if (ImGui.Button("Remove from party"))
            _macroNote = ChatMacro.Run(["/pcmd kick"]) ? null : "Could not remove the target from the party.";
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Game Start Macro"))
            _macroNote = ChatMacro.Run(plugin.Config.GameStartLines()) ? null : "Could not run the game start macro.";

        if (plugin.TryTarget(out PartyPresence presence))
        {
            Participant? match = PartyMatcher.Find(snapshot, presence.Name, presence.World);
            if (match != null)
                _selectedId = match.Id;
        }

        const float comboWidth = 280f;
        float right = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - comboWidth;
        if (right > ImGui.GetCursorPosX())
            ImGui.SameLine(right);
        Participant? current = snapshot.Participant(_selectedId ?? "");
        string preview = current == null ? "Select the challenger" : $"{current.FullName}  ·  {current.World}";
        ImGui.SetNextItemWidth(comboWidth);
        if (!ImGui.BeginCombo("##challenger", preview))
            return;

        foreach (Participant participant in snapshot.Participants)
        {
            if (ImGui.Selectable($"{participant.FullName}  ·  {participant.World}", participant.Id == _selectedId))
                _selectedId = participant.Id;
        }

        ImGui.EndCombo();
    }

    private void DrawMacroBar(Plugin plugin, SessionSnapshot snapshot)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Macros");
        ImGui.SameLine();
        foreach (string id in plugin.Config.GodHotbar.ToList())
        {
            GodMacro? macro = snapshot.Macros.FirstOrDefault(item => item.Id == id);
            if (macro == null)
                continue;
            if (ImGui.Button($"{macro.Name}##hot{id}"))
                RunShared(plugin, macro);
            ImGui.SameLine();
        }

        float libraryWidth = ImGui.CalcTextSize("Shared Library").X + 24f;
        float right = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - libraryWidth;
        if (right > ImGui.GetCursorPosX())
            ImGui.SameLine(right);
        if (ImGui.Button("Shared Library"))
            plugin.OpenLibrary();
    }

    private void RunShared(Plugin plugin, GodMacro macro)
    {
        string[] lines = macro.Text
            .Replace("\r", "", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _macroNote = ChatMacro.Run(lines) ? null : $"Could not run {macro.Name}.";
    }

    private void DrawStatus(Participant challenger)
    {
        int cleared = 0;
        TrialAspect[] trials = Enum.GetValues<TrialAspect>();
        foreach (TrialAspect trial in trials)
        {
            if (TrialRules.Victor(challenger, trial) != null)
                cleared++;
        }

        float target = cleared / (float)trials.Length;
        _progress += (target - _progress) * Math.Clamp(ImGui.GetIO().DeltaTime * 5f, 0f, 1f);

        ImGui.AlignTextToFramePadding();
        Ui.ThreadMark(18f);
        ImGui.SameLine(0, 4);
        ImGui.Text(challenger.Threads.ToString());
        ImGui.SameLine(0, 10);
        Ui.RunMark(18f);
        ImGui.SameLine(0, 4);
        ImGui.Text(Math.Max(challenger.Level, 1).ToString());
        ImGui.SameLine(0, 12);

        Vector2 origin = ImGui.GetCursorScreenPos();
        float width = Math.Max(40f, ImGui.GetContentRegionAvail().X);
        const float height = 8f;
        var draw = ImGui.GetWindowDrawList();
        var end = origin + new Vector2(width, height);
        draw.AddRectFilled(origin, end, ImGui.GetColorU32(new Vector4(0.08f, 0.07f, 0.12f, 1f)), 4f);
        float slot = width / trials.Length;
        for (int index = 0; index < trials.Length; index++)
        {
            bool done = TrialRules.Victor(challenger, trials[index]) != null;
            Vector4 color = Ui.TrialColor(trials[index]).Fill with { W = done ? 1f : 0.18f };
            var min = origin + new Vector2(index * slot, 0);
            var max = min + new Vector2(slot - 2f, height);
            draw.AddRectFilled(min, max, ImGui.GetColorU32(color), 3f);
        }

        float sheen = (float)ImGui.GetTime() * 80f % Math.Max(width, 1f);
        float filled = width * _progress;
        if (filled > 4f)
        {
            float x = Math.Min(sheen, filled - 18f);
            draw.AddRectFilled(
                origin + new Vector2(x, 1f),
                origin + new Vector2(Math.Min(x + 18f, filled), height - 1f),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.28f)),
                3f);
        }

        ImGui.Dummy(new Vector2(width, height));
    }
}
