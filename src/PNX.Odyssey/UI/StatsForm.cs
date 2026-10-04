using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Pnx.Odyssey.Core;
using Pnx.Odyssey.Services;

namespace Pnx.Odyssey.UI;

internal sealed class StatsForm
{
    private string? _exportNote;

    public void Draw(Plugin plugin)
    {
        SessionSnapshot? snapshot = plugin.Client.Snapshot;
        if (snapshot == null)
        {
            Ui.Hint("Join a session to see statistics.");
            return;
        }

        string session = snapshot.Id;
        List<VenuePerson> seen = VenueWatch.Book(plugin.Config, session);
        List<VenueTouch> dice = VenueWatch.DiceBook(plugin.Config, session);
        SessionStats stats = SessionStats.Build(snapshot, plugin.Client.Lines, seen, plugin.VenueHere, dice);

        if (ImGui.Button("Export CSV pack"))
            _exportNote = Export(plugin, snapshot, seen, dice);
        ImGui.SameLine();
        if (ImGui.Button("Copy players CSV"))
        {
            IReadOnlyDictionary<string, string> pack = SessionStats.BuildCsvPack(
                snapshot, plugin.Client.Lines, seen, plugin.VenueHere, dice);
            ImGui.SetClipboardText(pack["players.csv"]);
            _exportNote = "Players CSV copied to clipboard.";
        }

        ImGui.SameLine();
        if (ImGui.Button("Copy claims CSV"))
        {
            IReadOnlyDictionary<string, string> pack = SessionStats.BuildCsvPack(
                snapshot, plugin.Client.Lines, seen, plugin.VenueHere, dice);
            ImGui.SetClipboardText(pack["claims.csv"]);
            _exportNote = "Claims CSV copied to clipboard.";
        }

        if (_exportNote != null)
            ImGui.TextColored(Ui.Amber, _exportNote);

        float height = Math.Max(120f, ImGui.GetContentRegionAvail().Y);
        ImGui.BeginChild("stats-scroll", new Vector2(0, height));

        DrawHighlights(stats);
        DrawNight(stats);
        DrawRoster(stats);
        DrawTrials(stats);
        DrawClaims(stats);
        DrawDice(stats);
        DrawLobby(stats);
        DrawVenue(stats);
        DrawStaff(stats);

        ImGui.EndChild();
    }

    private static void DrawHighlights(SessionStats stats)
    {
        Ui.Section("Highlights");
        if (stats.Highlights.Count == 0)
        {
            Ui.Hint("Register a few players and the fun numbers will show up.");
            return;
        }

        foreach (string line in stats.Highlights)
            ImGui.BulletText(line);
    }

    private static void DrawNight(SessionStats stats)
    {
        Ui.Section("Tonight");
        Row("Session", stats.SessionName);
        Row("Age", SessionTime.Format(stats.Age));
        Row("Staff seated / connected", $"{stats.StaffSeated} / {stats.StaffConnected}");
        Row("Shared macros", $"{stats.SharedMacros} from {stats.MacroAuthors} author{(stats.MacroAuthors == 1 ? "" : "s")}");
    }

    private static void DrawRoster(SessionStats stats)
    {
        Ui.Section("Roster");
        Row("Registered", $"{stats.Registered}");
        Row("Still fighting", $"{stats.ActivePlayers}");
        Row("Shades (0 threads)", $"{stats.Shades}");
        Row("Discord on file", $"{stats.WithDiscord}  ({Pct(stats.WithDiscord, stats.Registered)})");
        Row("Threads in play", $"{stats.TotalThreads}  ·  avg {stats.AverageThreads:0.#}  ·  median {stats.MedianThreads:0.#}");
        Row("Gil represented", $"{stats.ThreadGilValue:N0}");
        Row("Run veterans (run 2+)", $"{stats.RunVeterans}");
        Row("Highest run", stats.MaxRun <= 0 ? "—" : stats.MaxRun.ToString(CultureInfo.InvariantCulture));
        Bars("Thread spread", stats.ThreadBuckets, stats.Registered);
        Bars("Home worlds", stats.Worlds, stats.Registered);
        Bars("Runs", stats.RunBuckets, stats.Registered);
    }

    private static void DrawTrials(SessionStats stats)
    {
        Ui.Section("Trials");
        Row("Strength cleared", $"{stats.StrengthCleared}  ({Pct(stats.StrengthCleared, stats.Registered)})");
        Row("Harmony cleared", $"{stats.HarmonyCleared}  ({Pct(stats.HarmonyCleared, stats.Registered)})");
        Row("Fear cleared", $"{stats.FearCleared}  ({Pct(stats.FearCleared, stats.Registered)})");
        Row("Power unlocked", $"{stats.PowerUnlocked}  ({Pct(stats.PowerUnlocked, stats.Registered)})");
        Row("Power cleared", $"{stats.PowerCleared}  ({Pct(stats.PowerCleared, stats.Registered)})");
        Row("Full Olympus clear", $"{stats.AllCleared}  ({Pct(stats.AllCleared, stats.Registered)})");
        Row("Ready for Zeus", $"{stats.OneTrialFromPower}");
        Bars("God win table", stats.VictorGods, Math.Max(1, stats.VictorGods.Sum(item => item.Count)));
    }

    private static void DrawClaims(SessionStats stats)
    {
        Ui.Section("Aspect claims");
        Row("Claims taken", $"{stats.Claims}");
        Row("Players with a claim", $"{stats.PlayersWithClaims}");
        Row("Offerings still open", $"{stats.OfferingsStillOpen}");
        Row("Finite claims left", $"{stats.FiniteClaimsLeft}");
        Bars("Popular offerings", stats.PopularOfferings, Math.Max(1, stats.Claims));
        Bars("Popular artists", stats.PopularArtists, Math.Max(1, stats.Claims));
    }

    private static void DrawDice(SessionStats stats)
    {
        Ui.Section("Dice");
        Row("Player faces rolled", $"{stats.PlayerDiceFaces}");
        Row("God faces rolled", $"{stats.GodDiceFaces}");
        Row("Complete player sets", $"{stats.CompleteRollSets}");
        Row("Avg player set total", stats.CompleteRollSets == 0 ? "—" : stats.AveragePlayerSetTotal.ToString("0.#", CultureInfo.InvariantCulture));
    }

    private static void DrawLobby(SessionStats stats)
    {
        Ui.Section("Lobby pulse");
        Row("Registers", $"{stats.RegisterEvents}");
        Row("Passes", $"{stats.PassEvents}");
        Row("Fails", $"{stats.FailEvents}");
        Row("Pass rate", stats.PassEvents + stats.FailEvents == 0
            ? "—"
            : Pct(stats.PassEvents, stats.PassEvents + stats.FailEvents));
        Row("Claims logged", $"{stats.ClaimEvents}");
        Row("New runs logged", $"{stats.RunEvents}");
    }

    private static void DrawVenue(SessionStats stats)
    {
        Ui.Section("Venue (this client)");
        Ui.Hint("Venue counts stay on this machine. They are not shared with the server.");
        Row("Unique people seen", $"{stats.VenueSeen}");
        Row("Here now", $"{stats.VenueHere}");
        Row("Seen but unregistered", $"{stats.VenueUnregistered}");
        Row("Registered and seen", $"{stats.VenueRegisteredSeen}");
        Row("Total re-entries", $"{stats.VenueTotalVisits}");
        Bars("Most seen visitors", stats.TopVisitors, Math.Max(1, stats.TopVisitors.Count == 0 ? 1 : stats.TopVisitors.Max(item => item.Count)));
    }

    private static void DrawStaff(SessionStats stats)
    {
        Ui.Section("Staff seats");
        Bars("Seats", stats.StaffRoles, Math.Max(1, stats.StaffSeated));
    }

    private static void Row(string label, string value)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Ui.Muted, label);
        ImGui.SameLine(220);
        ImGui.TextUnformatted(value);
    }

    private static void Bars(string title, IReadOnlyList<SessionStats.NamedCount> rows, int whole)
    {
        if (rows.Count == 0 || rows.All(item => item.Count == 0))
            return;

        ImGui.Spacing();
        ImGui.TextColored(Ui.Purple, title);
        float width = Math.Max(80f, ImGui.GetContentRegionAvail().X);
        foreach (SessionStats.NamedCount row in rows.Where(item => item.Count > 0))
        {
            ImGui.Text($"{row.Name}    {row.Count}");
            float fill = whole <= 0 ? 0f : Math.Clamp(row.Count / (float)whole, 0f, 1f);
            Vector2 origin = ImGui.GetCursorScreenPos();
            const float height = 6f;
            var draw = ImGui.GetWindowDrawList();
            draw.AddRectFilled(origin, origin + new Vector2(width, height), ImGui.GetColorU32(new Vector4(0.10f, 0.08f, 0.14f, 1f)), 3f);
            if (fill > 0.01f)
            {
                draw.AddRectFilled(
                    origin,
                    origin + new Vector2(width * fill, height),
                    ImGui.GetColorU32(new Vector4(0.58f, 0.46f, 0.82f, 0.85f)),
                    3f);
            }

            ImGui.Dummy(new Vector2(width, height + 2f));
        }
    }

    private static string Pct(int part, int whole) =>
        whole <= 0 ? "0%" : $"{Math.Round(100.0 * part / whole):0}%";

    private static string Export(Plugin plugin, SessionSnapshot snapshot, List<VenuePerson> seen, List<VenueTouch> dice)
    {
        try
        {
            IReadOnlyDictionary<string, string> pack = SessionStats.BuildCsvPack(
                snapshot, plugin.Client.Lines, seen, plugin.VenueHere, dice);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string safeName = Sanitize(snapshot.Name);
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PNX Odyssey",
                "exports",
                $"{safeName}-{stamp}");
            Directory.CreateDirectory(dir);
            foreach ((string name, string content) in pack)
                File.WriteAllText(Path.Combine(dir, name), content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true,
                });
            }
            catch (Exception)
            {
                // Opening the folder is optional.
            }

            return $"Exported {pack.Count} CSV files to {dir}";
        }
        catch (Exception ex)
        {
            return $"Export failed: {ex.Message}";
        }
    }

    private static string Sanitize(string name)
    {
        char[] bad = Path.GetInvalidFileNameChars();
        var buffer = new StringBuilder(name.Length);
        foreach (char ch in name.Trim())
            buffer.Append(bad.Contains(ch) ? '-' : ch);
        string clean = buffer.ToString().Trim();
        return clean.Length == 0 ? "session" : clean;
    }
}
