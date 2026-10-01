using System.Numerics;
using Dalamud.Bindings.ImGui;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.UI;

internal sealed class SessionForm
{
    private string _name = "";
    private string _password = "";
    private string? _closingId;
    private string _closePassword = "";

    public void Draw(Plugin plugin)
    {
        OdysseyClient client = plugin.Client;
        if (!client.UsingServer)
            Ui.Hint("Local table. It clears when the plugin reloads.");

        Ui.Section("New session");
        ImGui.SetNextItemWidth(280);
        ImGui.InputText("Name", ref _name, 40);
        ImGui.SetNextItemWidth(280);
        ImGui.InputText("Close password", ref _password, 64, ImGuiInputTextFlags.Password);
        Ui.Hint("The password is only used to close the session.");
        ImGui.BeginDisabled(string.IsNullOrWhiteSpace(_name) || _password.Length == 0);
        if (ImGui.Button("Create session"))
        {
            client.CreateSession(_name.Trim(), _password);
            _password = "";
        }
        ImGui.EndDisabled();

        Ui.Section("Open sessions");
        if (client.Sessions.Count == 0)
        {
            Ui.Hint("No open sessions.");
            return;
        }

        foreach (SessionSummary session in client.Sessions)
        {
            ImGui.PushID(session.Id);
            ImGui.AlignTextToFramePadding();
            ImGui.Text(session.Name);
            ImGui.SameLine();
            ImGui.TextColored(Ui.Muted, $"{session.LiveCount} live");
            ImGui.SameLine();
            ImGui.TextColored(Ui.Muted, SessionTime.Format(SessionTime.Elapsed(session.CreatedAt)));
            ImGui.SameLine();
            if (ImGui.Button("Join"))
                client.Join(session.Id);
            ImGui.SameLine();
            if (ImGui.Button("Close"))
            {
                _closingId = _closingId == session.Id ? null : session.Id;
                _closePassword = "";
            }

            if (_closingId == session.Id)
            {
                ImGui.SetNextItemWidth(180);
                ImGui.InputText("Password", ref _closePassword, 64, ImGuiInputTextFlags.Password);
                ImGui.SameLine();
                ImGui.BeginDisabled(_closePassword.Length == 0);
                if (ImGui.Button("Confirm close"))
                {
                    client.Close(session.Id, _closePassword);
                    _closePassword = "";
                    _closingId = null;
                }
                ImGui.EndDisabled();
            }

            ImGui.PopID();
        }
    }
}
