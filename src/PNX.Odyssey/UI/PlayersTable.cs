using System.Numerics;
using Dalamud.Bindings.ImGui;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.UI;

internal static class PlayersTable
{
    private static string? _removingId;
    private static string _password = "";
    private static string? _editingId;
    private static string _first = "";
    private static string _last = "";
    private static string _world = "";
    private static int _threads;
    private static StaffRole _strength;
    private static StaffRole _harmony;
    private static StaffRole _fear;
    private static StaffRole _power;
    private static bool _openEdit;
    public static void Draw(Plugin plugin, SessionSnapshot snapshot, bool fate)
    {
        IEnumerable<string> viewers = snapshot.Locks
            .Where(edit => edit.ExpiresAt > DateTime.UtcNow)
            .Select(edit =>
            {
                StaffRole role = snapshot.Member(edit.HolderId)?.Role ?? StaffRole.None;
                string seat = role == StaffRole.None ? "" : $" ({StaffText.Label(role)})";
                string you = string.Equals(edit.HolderId, plugin.Client.SelfId, StringComparison.OrdinalIgnoreCase) ? " · you" : "";
                return $"{edit.HolderName}{seat}{you}";
            })
            .Distinct(StringComparer.OrdinalIgnoreCase);
        string watching = string.Join(",  ", viewers);
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Who's viewing this");
        ImGui.SameLine();
        if (watching.Length == 0)
            Ui.Hint("—");
        else
            ImGui.TextColored(Ui.Gold, watching);
        ImGui.SameLine();
        int pixels = Math.Clamp(plugin.Config.TableTextSize, 12, 24);
        float sizeWidth = 140f;
        float sizeX = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - sizeWidth - ImGui.CalcTextSize("Text size").X;
        if (sizeX > ImGui.GetCursorPosX())
            ImGui.SameLine(sizeX);
        ImGui.SetNextItemWidth(sizeWidth);
        if (ImGui.SliderInt("Text size", ref pixels, 12, 24))
        {
            plugin.Config.TableTextSize = pixels;
            plugin.Config.Save();
        }

        float listHeight = Math.Max(48f, ImGui.GetContentRegionAvail().Y);
        ImGui.BeginChild("players-scroll", new Vector2(0, listHeight));
        using (Ui.PushPixels(pixels, false))
        {
            Vector2 cell = new(ImGui.GetFontSize() * 0.4f, ImGui.GetFontSize() * 0.2f);
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, cell);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(cell.X, cell.Y));
            DrawGrid(plugin, snapshot, fate);
            ImGui.PopStyleVar(2);
            DrawRemoval(plugin, snapshot);
        }

        ImGui.EndChild();
        if (_openEdit)
        {
            ImGui.OpenPopup("edit-player");
            _openEdit = false;
        }

        DrawEditor(plugin, snapshot);
    }

    private static void DrawGrid(Plugin plugin, SessionSnapshot snapshot, bool fate)
    {
        float gap = ImGui.GetFontSize() * 0.75f;
        float tick = ImGui.GetFontSize() * 0.85f;
        float trialWidth = ImGui.CalcTextSize("Aphrodite").X + tick + (gap * 2f);
        float threadsWidth = Math.Max(ImGui.CalcTextSize("Threads").X, ImGui.CalcTextSize("10").X) + gap;
        float runWidth = Math.Max(ImGui.CalcTextSize("Run").X, ImGui.CalcTextSize("10").X) + gap;
        float completeWidth = ImGui.CalcTextSize("Complete").X + gap;
        float actionsWidth = ImGui.CalcTextSize("X").X + ImGui.CalcTextSize("Target").X + ImGui.CalcTextSize("Unmark").X + ImGui.CalcTextSize("Edit").X + (gap * 5f);

        if (!ImGui.BeginTable("players", 11, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit, new Vector2(0, 0)))
            return;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("First", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Last", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Threads", ImGuiTableColumnFlags.WidthFixed, threadsWidth);
        ImGui.TableSetupColumn("Strength", ImGuiTableColumnFlags.WidthFixed, trialWidth);
        ImGui.TableSetupColumn("Harmony", ImGuiTableColumnFlags.WidthFixed, trialWidth);
        ImGui.TableSetupColumn("Fear", ImGuiTableColumnFlags.WidthFixed, trialWidth);
        ImGui.TableSetupColumn("Power", ImGuiTableColumnFlags.WidthFixed, trialWidth);
        ImGui.TableSetupColumn("Run", ImGuiTableColumnFlags.WidthFixed, runWidth);
        ImGui.TableSetupColumn("Complete", ImGuiTableColumnFlags.WidthFixed, completeWidth);
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, actionsWidth);
        DrawHeaders();

        foreach (Participant participant in snapshot.Participants)
        {
            ImGui.TableNextRow();
            bool locked = Ui.Foreign(plugin.Client, participant.Id, out _);
            if (locked)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.45f, 0.3f, 0.1f, 0.55f)));
            else if (participant.IsShade)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.35f, 0.12f, 0.12f, 0.4f)));

            Cell(participant.IsShade ? $"{participant.FirstName}  Shade" : participant.FirstName, participant.IsShade);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(participant.IsShade ? "Shade  ·  0 threads" : $"{participant.Threads} threads");
            Cell(participant.LastName, false);
            Cell(participant.World, false);
            Cell(participant.Threads.ToString(), false);
            Victor(participant.Strength, TrialAspect.Strength);
            Victor(participant.Harmony, TrialAspect.Harmony);
            Victor(participant.Fear, TrialAspect.Fear);
            Victor(participant.Power, TrialAspect.Power);
            Cell(Math.Max(participant.Level, 1).ToString(), false);

            ImGui.TableNextColumn();
            bool ready = fate && TrialRules.AllTrialsCleared(participant);
            ImGui.BeginDisabled(!ready);
            if (ImGui.Button($"Complete##{participant.Id}"))
                plugin.Client.Complete(participant.Id, participant.Revision);
            ImGui.EndDisabled();
            if (!ready && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Available after Strength, Harmony, Fear, and Power are all cleared. Threads stay. The run number goes up.");

            ImGui.TableNextColumn();
            ImGui.PushID(participant.Id);
            if (fate && ImGui.SmallButton("Edit"))
                OpenEditor(participant);
            if (fate)
                ImGui.SameLine();
            if (ImGui.SmallButton("X"))
            {
                _removingId = participant.Id;
                _password = "";
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("Target"))
                plugin.TargetParticipant(participant);
            ImGui.SameLine();
            if (ImGui.SmallButton(plugin.IsMarked(participant.Id) ? "Unmark" : "Mark"))
                plugin.ToggleMark(participant.Id);
            ImGui.PopID();
        }

        ImGui.EndTable();
        DrawRemoval(plugin, snapshot);
    }

    private static void DrawRemoval(Plugin plugin, SessionSnapshot snapshot)
    {
        if (_removingId == null)
            return;

        Participant? participant = snapshot.Participant(_removingId);
        if (participant == null)
        {
            _removingId = null;
            return;
        }

        ImGui.Separator();
        ImGui.Text($"Remove {participant.FullName}. Enter the session password.");
        ImGui.SetNextItemWidth(ImGui.CalcTextSize("Session password").X * 1.4f);
        ImGui.InputText("Password##remove", ref _password, 64, ImGuiInputTextFlags.Password);
        ImGui.BeginDisabled(_password.Length == 0);
        if (ImGui.Button("Confirm remove"))
        {
            plugin.Client.Remove(participant.Id, _password);
            _password = "";
            _removingId = null;
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Cancel remove"))
        {
            _password = "";
            _removingId = null;
        }
    }

    private static void OpenEditor(Participant participant)
    {
        _editingId = participant.Id;
        _first = participant.FirstName;
        _last = participant.LastName;
        _world = participant.World;
        _threads = participant.Threads;
        _strength = participant.Strength ?? StaffRole.None;
        _harmony = participant.Harmony ?? StaffRole.None;
        _fear = participant.Fear ?? StaffRole.None;
        _power = participant.Power ?? StaffRole.None;
        _openEdit = true;
    }

    private static void DrawEditor(Plugin plugin, SessionSnapshot snapshot)
    {
        if (_editingId == null)
            return;

        Participant? participant = snapshot.Participant(_editingId);
        if (participant == null)
        {
            _editingId = null;
            return;
        }

        ImGui.SetNextWindowSize(new Vector2(460, 360), ImGuiCond.Appearing);
        bool open = true;
        if (!ImGui.BeginPopupModal("edit-player", ref open))
        {
            if (!open)
                _editingId = null;
            return;
        }

        ImGui.SetNextItemWidth(180);
        ImGui.InputText("First", ref _first, 32);
        ImGui.SetNextItemWidth(180);
        ImGui.InputText("Last", ref _last, 32);
        ImGui.SetNextItemWidth(180);
        ImGui.InputText("World", ref _world, 32);
        ImGui.SetNextItemWidth(180);
        ImGui.SliderInt("Threads", ref _threads, 0, 10);
        VictorCombo("Strength", TrialAspect.Strength, ref _strength);
        VictorCombo("Harmony", TrialAspect.Harmony, ref _harmony);
        VictorCombo("Fear", TrialAspect.Fear, ref _fear);
        VictorCombo("Power", TrialAspect.Power, ref _power);
        ImGui.BeginDisabled();
        ImGui.Text($"Run {Math.Max(participant.Level, 1)}");
        ImGui.EndDisabled();
        if (ImGui.Button("Save"))
        {
            plugin.Client.EditParticipant(participant, _first, _last, _world, _threads, _strength, _harmony, _fear, _power);
            _editingId = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel") || !open)
        {
            _editingId = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private static void VictorCombo(string label, TrialAspect aspect, ref StaffRole role)
    {
        string preview = role == StaffRole.None ? "—" : StaffText.Label(role);
        ImGui.SetNextItemWidth(180);
        if (!ImGui.BeginCombo(label, preview))
            return;

        if (ImGui.Selectable("—", role == StaffRole.None))
            role = StaffRole.None;
        foreach (StaffRole seat in Enum.GetValues<StaffRole>())
        {
            if (StaffText.AspectOf(seat) != aspect)
                continue;
            if (ImGui.Selectable(StaffText.Label(seat), role == seat))
                role = seat;
        }

        ImGui.EndCombo();
    }

    private static void DrawHeaders()
    {
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        string[] labels = ["First", "Last", "World", "Threads", "Strength", "Harmony", "Fear", "Power", "Run", "Complete", "Actions"];
        TrialAspect?[] aspects =
        [
            null, null, null, null,
            TrialAspect.Strength, TrialAspect.Harmony, TrialAspect.Fear, TrialAspect.Power,
            null, null, null,
        ];
        for (int column = 0; column < labels.Length; column++)
        {
            ImGui.TableSetColumnIndex(column);
            if (aspects[column] is not TrialAspect aspect)
            {
                ImGui.TableHeader(labels[column]);
                continue;
            }

            Ui.TrialPaint paint = Ui.TrialColor(aspect);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(paint.Fill));
            float width = ImGui.GetColumnWidth();
            float textWidth = ImGui.CalcTextSize(labels[column]).X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (width - textWidth) * 0.5f));
            ImGui.PushStyleColor(ImGuiCol.Text, paint.Ink);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(labels[column]);
            ImGui.PopStyleColor();
        }
    }

    private static void Victor(StaffRole? role, TrialAspect aspect)
    {
        ImGui.TableNextColumn();
        Ui.TrialPaint paint = Ui.TrialColor(aspect);
        ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(paint.Fill with { W = 0.16f }));
        if (role is null or StaffRole.None)
        {
            ImGui.TextColored(Ui.Muted, "—");
            return;
        }

        ImGui.TextColored(Ui.Gold, StaffText.Label(role.Value));
        ImGui.SameLine(0, 4);
        float mark = ImGui.GetFontSize() * 0.8f;
        Vector2 origin = ImGui.GetCursorScreenPos();
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        uint ink = ImGui.GetColorU32(Ui.Good);
        draw.AddLine(origin + new Vector2(mark * 0.12f, mark * 0.52f), origin + new Vector2(mark * 0.38f, mark * 0.84f), ink, Math.Max(1.4f, mark * 0.16f));
        draw.AddLine(origin + new Vector2(mark * 0.38f, mark * 0.84f), origin + new Vector2(mark * 0.9f, mark * 0.16f), ink, Math.Max(1.4f, mark * 0.16f));
        ImGui.Dummy(new Vector2(mark, ImGui.GetFontSize()));
    }

    private static void Cell(string text, bool muted)
    {
        ImGui.TableNextColumn();
        if (muted) ImGui.TextColored(Ui.Muted, text);
        else ImGui.TextUnformatted(text);
    }
}
