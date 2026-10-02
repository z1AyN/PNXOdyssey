using System.Numerics;
using Dalamud.Bindings.ImGui;
using Pnx.Odyssey.Core;
using Pnx.Odyssey.Services;

namespace Pnx.Odyssey.UI;

internal sealed class WatcherForm
{
    public void Draw(Plugin plugin)
    {
        SessionSnapshot? snapshot = plugin.Client.Snapshot;
        string session = snapshot?.Id ?? "local";
        List<VenuePerson> seen = VenueWatch.Book(plugin.Config, session);
        float height = Math.Max(120f, ImGui.GetContentRegionAvail().Y);
        ImGui.BeginChild("watcher-scroll", new Vector2(0, height));
        ImGui.Text($"Unique people seen    {seen.Count}");
        Ui.Hint("This is who this game client has seen. It is not shared with the server.");

        DrawHere(plugin, seen);
        DrawMissing(plugin, snapshot, seen);
        DrawRegistered(plugin, snapshot);
        ImGui.EndChild();
    }

    private static void DrawHere(Plugin plugin, List<VenuePerson> seen)
    {
        Ui.Section("Here now");
        int count = 0;
        foreach (VenuePerson person in seen.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!plugin.VenueHere.Contains(VenueWatch.Key(person.Name, person.World)))
                continue;
            count++;
            ImGui.Text($"{person.Name}  ·  {person.World}");
        }

        if (count == 0)
            Ui.Hint("No other players are visible.");
        else
            Ui.Hint($"{count} visible.");
    }

    private static void DrawMissing(Plugin plugin, SessionSnapshot? snapshot, List<VenuePerson> seen)
    {
        Ui.Section("Not registered");
        if (snapshot == null)
        {
            Ui.Hint("Join a session to compare this with the roster.");
            return;
        }

        int count = 0;
        foreach (VenuePerson person in seen.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (snapshot.Participants.Any(player => PartyMatcher.SamePerson(person.Name, person.World, player)))
                continue;
            count++;
            bool here = plugin.VenueHere.Contains(VenueWatch.Key(person.Name, person.World));
            ImGui.Text($"{person.Name}  ·  {person.World}");
            ImGui.SameLine();
            ImGui.TextColored(here ? Ui.Good : Ui.Muted, here ? "here" : "left");
        }

        if (count == 0)
            Ui.Hint("Everyone seen is registered.");
    }

    private static void DrawRegistered(Plugin plugin, SessionSnapshot? snapshot)
    {
        Ui.Section("Registered");
        if (snapshot == null)
        {
            Ui.Hint("Join a session to see trial interactions.");
            return;
        }

        List<VenueTouch> dice = VenueWatch.DiceBook(plugin.Config, snapshot.Id);
        if (snapshot.Participants.Count == 0)
        {
            Ui.Hint("No players are registered.");
            return;
        }

        foreach (Participant player in snapshot.Participants.OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase))
        {
            (DateTime at, int count) = Interaction(plugin, snapshot, dice, player);
            string when = count == 0
                ? "no trial interaction yet"
                : $"{at.ToLocalTime():HH:mm:ss}   {Ago(at)}   {count} interaction{(count == 1 ? "" : "s")}";
            ImGui.Text(player.FullName);
            ImGui.SameLine();
            ImGui.TextColored(Ui.Muted, when);
        }
    }

    private static (DateTime At, int Count) Interaction(Plugin plugin, SessionSnapshot snapshot, List<VenueTouch> dice, Participant player)
    {
        DateTime latest = DateTime.MinValue;
        int count = 0;
        foreach (LobbyLine line in plugin.Client.Lines)
        {
            if (!Counts(line, player))
                continue;
            count++;
            if (line.At > latest)
                latest = line.At;
        }

        VenueTouch? touch = dice.FirstOrDefault(item => string.Equals(item.ParticipantId, player.Id, StringComparison.OrdinalIgnoreCase));
        if (touch != null && touch.Count > 0)
        {
            count += touch.Count;
            if (touch.At > latest)
                latest = touch.At;
        }

        return (latest, count);
    }

    private static bool Counts(LobbyLine line, Participant player)
    {
        if (line.Kind is not (LobbyKind.Register or LobbyKind.Threads or LobbyKind.Run or LobbyKind.Pass or LobbyKind.Fail or LobbyKind.Claim))
            return false;
        return line.Text.Contains(player.FullName, StringComparison.OrdinalIgnoreCase);
    }

    private static string Ago(DateTime at)
    {
        TimeSpan span = DateTime.UtcNow - (at.Kind == DateTimeKind.Utc ? at : at.ToUniversalTime());
        if (span < TimeSpan.Zero)
            span = TimeSpan.Zero;
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h {span.Minutes:00}m ago";
        if (span.TotalMinutes >= 1)
            return $"{(int)span.TotalMinutes}m {span.Seconds:00}s ago";
        return $"{Math.Max(0, span.Seconds)}s ago";
    }
}
