using System.Numerics;
using Dalamud.Bindings.ImGui;
using Pnx.Odyssey.Core;
using Pnx.Odyssey.Services;

namespace Pnx.Odyssey.UI;

internal sealed class AspectsForm
{
    private const string YellMark = "\uE0C0";

    private bool _claimed;
    private string? _removingId;
    private string _password = "";
    private string? _note;
    private readonly Dictionary<string, string> _choice = new(StringComparer.Ordinal);

    public void Draw(Plugin plugin, SessionSnapshot snapshot)
    {
        bool available = !_claimed;
        if (available)
            PushSelected();
        if (ImGui.Button("Available"))
            _claimed = false;
        if (available)
            ImGui.PopStyleColor(3);

        ImGui.SameLine();
        if (!available)
            PushSelected();
        if (ImGui.Button("Claimed"))
            _claimed = true;
        if (!available)
            ImGui.PopStyleColor(3);

        if (_note != null)
            ImGui.TextColored(Ui.Amber, _note);

        float reserved = _claimed && _removingId != null ? ImGui.GetFrameHeightWithSpacing() * 4f : 0f;
        float height = Math.Max(80f, ImGui.GetContentRegionAvail().Y - reserved);
        int pixels = Math.Clamp(plugin.Config.TableTextSize, 12, 24);
        ImGui.BeginChild("aspects-scroll", new Vector2(0, height));
        using (Ui.PushPixels(pixels, false))
        {
            if (_claimed)
                DrawClaimed(plugin, snapshot);
            else
                DrawAvailable(plugin, snapshot);
        }

        ImGui.EndChild();
        if (_claimed)
            DrawRemoval(plugin, snapshot);
    }

    private void DrawAvailable(Plugin plugin, SessionSnapshot snapshot)
    {
        float claims = ImGui.CalcTextSize("Shared pool: 2 rainbow claims").X + 16f;
        float player = 240f;
        float action = ImGui.CalcTextSize("Claim").X + 24f;
        if (!ImGui.BeginTable("aspects-available", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 170f);
        ImGui.TableSetupColumn("Offering", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Claims", ImGuiTableColumnFlags.WidthFixed, claims);
        ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthFixed, player);
        ImGui.TableSetupColumn("##claim", ImGuiTableColumnFlags.WidthFixed, action);
        ImGui.TableHeadersRow();

        foreach (AspectCatalog.Offering offering in AspectCatalog.All)
        {
            ImGui.TableNextRow();
            Cell(offering.Name);
            ImGui.TableNextColumn();
            ImGui.TextWrapped(offering.Text);
            Cell(AspectCatalog.ClaimsText(offering, snapshot.Claims));
            ImGui.TableNextColumn();
            string picked = _choice.TryGetValue(offering.Id, out string? id) ? id : "";
            if (snapshot.Participant(picked) == null)
                picked = "";
            Participant? selected = picked.Length == 0 ? null : snapshot.Participant(picked);
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo($"##who-{offering.Id}", selected == null ? "Registered player" : Label(selected)))
            {
                foreach (Participant participant in snapshot.Participants.OrderBy(person => person.FullName, StringComparer.OrdinalIgnoreCase))
                {
                    if (ImGui.Selectable($"{Label(participant)}##{participant.Id}", participant.Id == picked))
                        _choice[offering.Id] = participant.Id;
                }

                ImGui.EndCombo();
            }

            ImGui.TableNextColumn();
            bool room = AspectCatalog.HasRoom(offering, snapshot.Claims);
            ImGui.BeginDisabled(!room || selected == null);
            if (ImGui.SmallButton($"Claim##{offering.Id}"))
                plugin.Client.ClaimAspect(offering.Id, selected!.Id);
            ImGui.EndDisabled();
            if (!room && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("No claims left.");
        }

        ImGui.EndTable();
    }

    private void DrawClaimed(Plugin plugin, SessionSnapshot snapshot)
    {
        float action = ImGui.CalcTextSize("Announce").X + 24f;
        float tell = ImGui.CalcTextSize("Tell").X + 20f;
        float remove = ImGui.CalcTextSize("X").X + 20f;
        float run = ImGui.CalcTextSize("Run 100").X + 16f;
        if (!ImGui.BeginTable("aspects-claimed", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Aspect", ImGuiTableColumnFlags.WidthFixed, 170f);
        ImGui.TableSetupColumn("Offering", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Run", ImGuiTableColumnFlags.WidthFixed, run);
        ImGui.TableSetupColumn("##announce", ImGuiTableColumnFlags.WidthFixed, action);
        ImGui.TableSetupColumn("##tell", ImGuiTableColumnFlags.WidthFixed, tell);
        ImGui.TableSetupColumn("##remove", ImGuiTableColumnFlags.WidthFixed, remove);
        ImGui.TableHeadersRow();

        if (snapshot.Claims.Count == 0)
        {
            ImGui.EndTable();
            Ui.Hint("No claims yet.");
            return;
        }

        foreach (AspectClaim claim in snapshot.Claims)
        {
            AspectCatalog.Offering? offering = AspectCatalog.Find(claim.OfferingId);
            ImGui.TableNextRow();
            Cell(claim.PlayerName);
            Cell(offering?.Name ?? "Unknown aspect");
            ImGui.TableNextColumn();
            ImGui.TextWrapped(offering?.Text ?? claim.OfferingId);
            Cell(Math.Max(claim.Run, 1).ToString());
            ImGui.TableNextColumn();
            if (ImGui.SmallButton($"Announce##{claim.Id}"))
                Announce(claim, offering);
            ImGui.TableNextColumn();
            Participant? winner = snapshot.Participant(claim.ParticipantId);
            string? discord = AspectCatalog.Discord(offering?.Name);
            ImGui.BeginDisabled(winner == null || discord == null);
            if (ImGui.SmallButton($"Tell##{claim.Id}"))
                Tell(claim, offering, winner!, discord!);
            ImGui.EndDisabled();
            if ((winner == null || discord == null) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(winner == null ? "That player is no longer registered." : "No Discord is listed for this aspect.");
            ImGui.TableNextColumn();
            if (ImGui.SmallButton($"X##{claim.Id}"))
            {
                _removingId = claim.Id;
                _password = "";
            }
        }

        ImGui.EndTable();
    }

    private void Announce(AspectClaim claim, AspectCatalog.Offering? offering)
    {
        string aspect = offering?.Name ?? "Unknown";
        string offeringText = offering?.Text ?? "their offering";
        int run = Math.Max(claim.Run, 1);
        string line = Fit(
            $"/yell {YellMark} {claim.PlayerName} has completed the Trials of Olympus (Run: #{run}) and has claimed the Aspect of {aspect}: {offeringText}",
            180);
        _note = ChatMacro.Run([line]) ? null : "Could not announce that claim.";
    }

    private static string Fit(string line, int bytes)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(line) <= bytes)
            return line;
        while (line.Length > 0 && System.Text.Encoding.UTF8.GetByteCount(line + "…") > bytes)
            line = line[..^1];
        return line.TrimEnd() + "…";
    }

    private void Tell(AspectClaim claim, AspectCatalog.Offering? offering, Participant winner, string discord)
    {
        string aspect = offering?.Name ?? "their Aspect";
        string offeringText = offering?.Text ?? "their offering";
        string target = $"{winner.FullName}@{winner.World}";
        var lines = new List<string>();
        lines.AddRange(Speak("/tell " + target, $"You claimed the Aspect of {aspect}."));
        lines.Add("/wait 1");
        lines.AddRange(Speak("/tell " + target, $"Their offering: {offeringText}. Reach them on Discord: {discord}"));
        _note = ChatMacro.Run(lines) ? null : $"Could not tell {winner.FullName}.";
    }

    private static List<string> Speak(string command, string message)
    {
        var lines = new List<string>();
        string prefix = command + " ";
        int room = 170 - System.Text.Encoding.UTF8.GetByteCount(prefix);
        if (room < 20)
            room = 20;
        string rest = message.Trim();
        while (rest.Length > 0)
        {
            int take = rest.Length;
            while (take > 0 && System.Text.Encoding.UTF8.GetByteCount(rest[..take]) > room)
                take--;
            if (take < rest.Length)
            {
                int space = rest.LastIndexOf(' ', take - 1, take);
                if (space > 12)
                    take = space;
            }

            lines.Add(prefix + rest[..take].Trim());
            rest = take >= rest.Length ? "" : rest[take..].TrimStart();
            if (lines.Count == 6)
                break;
        }

        return lines;
    }

    private void DrawRemoval(Plugin plugin, SessionSnapshot snapshot)
    {
        if (_removingId == null)
            return;

        AspectClaim? claim = snapshot.Claims.FirstOrDefault(item => item.Id == _removingId);
        if (claim == null)
        {
            _removingId = null;
            return;
        }

        AspectCatalog.Offering? offering = AspectCatalog.Find(claim.OfferingId);
        ImGui.Separator();
        ImGui.Text($"Remove {claim.PlayerName}'s claim of {offering?.Name ?? "this aspect"}. Enter the session password.");
        ImGui.SetNextItemWidth(ImGui.CalcTextSize("Session password").X * 1.4f);
        ImGui.InputText("Password##aspect", ref _password, 64, ImGuiInputTextFlags.Password);
        ImGui.BeginDisabled(_password.Length == 0);
        if (ImGui.Button("Confirm remove"))
        {
            plugin.Client.RemoveAspect(claim.Id, _password);
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

    private static string Label(Participant participant) => $"{participant.FullName}  ·  {participant.World}";

    private static void Cell(string text)
    {
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(text);
    }

    private static void PushSelected()
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.36f, 0.28f, 0.55f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.46f, 0.36f, 0.68f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.56f, 0.44f, 0.78f, 1f));
    }
}
