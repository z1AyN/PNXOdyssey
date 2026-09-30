using System.Numerics;
using Dalamud.Bindings.ImGui;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.UI;

internal sealed class RegisterForm
{
    private string _first = "";
    private string _last = "";
    private string _world = "";
    private string _discord = "";
    private int _threads = TrialRules.StartingThreads;
    private int _level;
    private string _followed = "";

    public string? Draw(Plugin plugin)
    {
        FollowTarget(plugin);
        SessionSnapshot? snapshot = plugin.Client.Snapshot;
        if (snapshot == null)
            return null;

        if (plugin.TryTarget(out PartyPresence target))
            Ui.Hint($"Target   {target.Name}   ·   {target.World}");
        else
            Ui.Hint("Target a player to fill this in.");
        Ui.Space();

        Participant? existing = Find(snapshot);
        if (existing != null)
        {
            DrawCard(existing);
            if (Ui.Foreign(plugin.Client, existing.Id, out string holder))
                Ui.Editing(holder);
            return EditScope.Register(existing.Id);
        }

        if (ImGui.BeginTable("register-fields", 2, ImGuiTableFlags.SizingStretchProp))
        {
            Field("First name", ref _first, 32);
            Field("Last name", ref _last, 32);
            Field("World", ref _world, 32);
            Field("Discord", ref _discord, 64);
            ImGui.EndTable();
        }

        Ui.Space();
        ImGui.SetNextItemWidth(140);
        ImGui.InputInt("Starting threads", ref _threads);
        _threads = Math.Clamp(_threads, TrialRules.MinStartingThreads, TrialRules.MaxThreads);
        Ui.Hint("Players begin with 4 threads. A completed run keeps that count and starts the next run.");

        Ui.Space();
        bool ready = _first.Trim().Length > 0 && _last.Trim().Length > 0 && _world.Trim().Length > 0;
        ImGui.BeginDisabled(!ready);
        if (ImGui.Button("Register##submit", new Vector2(160, 32)))
            plugin.Client.Register(_first, _last, _world, _discord, _threads, 1);
        ImGui.EndDisabled();
        return null;
    }

    private static void Field(string label, ref string value, int max)
    {
        ImGui.TableNextColumn();
        ImGui.TextColored(Ui.Muted, label);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText($"##{label}", ref value, max);
    }

    private void FollowTarget(Plugin plugin)
    {
        if (!plugin.TryTarget(out PartyPresence target))
            return;

        string key = $"{target.Name}@{target.World}";
        if (key == _followed)
            return;

        _followed = key;
        (_first, _last) = CharacterNames.Split(target.Name);
        _world = target.World;
        _level = target.Level;
        _threads = TrialRules.StartingThreads;
        _discord = "";
    }

    private Participant? Find(SessionSnapshot snapshot)
    {
        if (_first.Length == 0 || _last.Length == 0)
            return null;

        string id = ParticipantIds.Create(_first, _last, _world);
        return snapshot.Participant(id);
    }

    private static void DrawCard(Participant participant)
    {
        ImGui.TextColored(Ui.Purple, "Already registered");
        ImGui.Text($"{participant.FullName}   ·   {participant.World}   ·   {participant.Threads} threads   ·   Run {Math.Max(participant.Level, 1)}");
    }
}
