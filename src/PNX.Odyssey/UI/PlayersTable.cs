using System.Numerics;
using Dalamud.Bindings.ImGui;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.UI;

internal static class PlayersTable
{
    private static string? _removingId;
    private static string _password = "";
    public static void Draw(Plugin plugin, SessionSnapshot snapshot, bool fate)
    {
        IEnumerable<string> editors = snapshot.Locks
            .Where(edit => edit.ExpiresAt > DateTime.UtcNow
                && !string.Equals(edit.HolderId, plugin.Client.SelfId, StringComparison.OrdinalIgnoreCase))
            .Select(edit => edit.HolderName)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (string name in editors)
            Ui.Editing(name);

        if (!ImGui.BeginTable("players", 11, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit, new Vector2(0, 0)))
            return;

        const float trialWidth = 108f;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("First", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Last", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Threads", ImGuiTableColumnFlags.WidthFixed, 64);
        ImGui.TableSetupColumn("Strength", ImGuiTableColumnFlags.WidthFixed, trialWidth);
        ImGui.TableSetupColumn("Harmony", ImGuiTableColumnFlags.WidthFixed, trialWidth);
        ImGui.TableSetupColumn("Fear", ImGuiTableColumnFlags.WidthFixed, trialWidth);
        ImGui.TableSetupColumn("Power", ImGuiTableColumnFlags.WidthFixed, trialWidth);
        ImGui.TableSetupColumn("Run", ImGuiTableColumnFlags.WidthFixed, 40);
        ImGui.TableSetupColumn("Complete", ImGuiTableColumnFlags.WidthFixed, 88);
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 150);
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
        ImGui.SetNextItemWidth(220);
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
        ImGui.SameLine(0, 6);
        ImGui.TextColored(Ui.Good, "✓");
    }

    private static void Cell(string text, bool muted)
    {
        ImGui.TableNextColumn();
        if (muted) ImGui.TextColored(Ui.Muted, text);
        else ImGui.TextUnformatted(text);
    }
}
