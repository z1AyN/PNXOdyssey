using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Pnx.Odyssey.Core;
using Pnx.Odyssey.Services;

namespace Pnx.Odyssey.UI;

internal sealed class MacrosForm
{
    private static readonly string[] Months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
    private readonly Dictionary<int, DateTime> _month = new();
    private string? _note;

    public void Draw(Plugin plugin)
    {
        long now = ServerClock.Unix();
        Ui.Hint($"Server clock  {ServerClock.Clock()}");
        if (_note != null)
            ImGui.TextColored(Ui.Amber, _note);

        float height = Math.Max(120f, ImGui.GetContentRegionAvail().Y);
        ImGui.BeginChild("macros-scroll", new Vector2(0, height));
        Ui.Section("Macros");
        for (int index = 0; index < plugin.Config.Macros.Count; index++)
        {
            if (DrawMacro(plugin, plugin.Config.Macros[index], now))
                break;
        }

        if (ImGui.Button("Add macro"))
        {
            plugin.Config.Macros.Add(new ShoutMacro
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = "New macro",
                Text = "/sh ",
            });
            plugin.Config.Save();
        }

        DrawDjs(plugin, now);
        ImGui.EndChild();
    }

    private void DrawDjs(Plugin plugin, long now)
    {
        Ui.Section("DJs");
        Ui.Hint("Start times use the server clock. The DJ shout follows whoever has already started.");
        List<DjEntry> djs = plugin.Config.Djs;
        for (int index = 0; index < djs.Count; index++)
        {
            DjEntry entry = djs[index];
            DateTime utc = Utc(entry.StartUnix);
            ImGui.PushID(index);
            if (ImGui.Button($"{utc:dd MMM yyyy}##date"))
                ImGui.OpenPopup("date");
            ImGui.SameLine();
            if (ImGui.Button($"{utc:HH:mm}##time"))
                ImGui.OpenPopup("time");
            if (ImGui.BeginPopup("date"))
            {
                DrawDate(plugin, entry, index);
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopup("time"))
            {
                DrawTime(plugin, entry);
                ImGui.EndPopup();
            }

            string name = entry.Name;
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            if (ImGui.InputText("##name", ref name, 48))
            {
                entry.Name = name;
                plugin.Config.Save();
            }

            string twitch = entry.Twitch;
            ImGui.SameLine();
            ImGui.SetNextItemWidth(260);
            if (ImGui.InputText("##twitch", ref twitch, 120))
            {
                entry.Twitch = twitch;
                plugin.Config.Save();
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("X"))
            {
                djs.RemoveAt(index);
                _month.Clear();
                plugin.Config.Save();
                ImGui.PopID();
                break;
            }

            ImGui.PopID();
        }

        if (ImGui.Button("Add DJ"))
        {
            long start = now - (now % 3600) + 3600;
            djs.Add(new DjEntry { Name = "DJ", Twitch = "https://www.twitch.tv/", StartUnix = start });
            plugin.Config.Save();
        }
    }

    private void DrawDate(Plugin plugin, DjEntry entry, int index)
    {
        DateTime current = Utc(entry.StartUnix);
        if (!_month.TryGetValue(index, out DateTime shown))
            shown = new DateTime(current.Year, current.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        if (ImGui.ArrowButton("prev", ImGuiDir.Left))
            shown = shown.AddMonths(-1);
        ImGui.SameLine();
        ImGui.Text($"{Months[shown.Month - 1]} {shown.Year}");
        ImGui.SameLine();
        if (ImGui.ArrowButton("next", ImGuiDir.Right))
            shown = shown.AddMonths(1);
        _month[index] = shown;

        ImGui.TextColored(Ui.Muted, "Mo  Tu  We  Th  Fr  Sa  Su");
        int pad = ((int)new DateTime(shown.Year, shown.Month, 1).DayOfWeek + 6) % 7;
        int days = DateTime.DaysInMonth(shown.Year, shown.Month);
        int column = 0;
        for (int blank = 0; blank < pad; blank++)
        {
            ImGui.Dummy(new Vector2(28, 22));
            ImGui.SameLine();
            column++;
        }

        for (int day = 1; day <= days; day++)
        {
            bool selected = current.Year == shown.Year && current.Month == shown.Month && current.Day == day;
            if (selected)
                PushSelected();
            if (ImGui.Button($"{day,2}##d{day}", new Vector2(28, 22)))
            {
                entry.StartUnix = Compose(shown.Year, shown.Month, day, current.Hour, current.Minute);
                plugin.Config.Save();
                ImGui.CloseCurrentPopup();
            }

            if (selected)
                ImGui.PopStyleColor(3);
            column++;
            if (column % 7 != 0 && day != days)
                ImGui.SameLine();
        }
    }

    private static void DrawTime(Plugin plugin, DjEntry entry)
    {
        DateTime current = Utc(entry.StartUnix);
        ImGui.Text("Hour");
        for (int hour = 0; hour < 24; hour++)
        {
            if (hour == current.Hour)
                PushSelected();
            if (ImGui.Button($"{hour:00}##h{hour}", new Vector2(36, 22)))
            {
                entry.StartUnix = Compose(current.Year, current.Month, current.Day, hour, current.Minute);
                plugin.Config.Save();
            }

            if (hour == current.Hour)
                ImGui.PopStyleColor(3);
            if (hour % 6 != 5)
                ImGui.SameLine();
        }

        ImGui.Text("Minute");
        for (int minute = 0; minute < 60; minute += 5)
        {
            bool selected = current.Minute / 5 * 5 == minute;
            if (selected)
                PushSelected();
            if (ImGui.Button($"{minute:00}##m{minute}", new Vector2(36, 22)))
            {
                entry.StartUnix = Compose(current.Year, current.Month, current.Day, current.Hour, minute);
                plugin.Config.Save();
                ImGui.CloseCurrentPopup();
            }

            if (selected)
                ImGui.PopStyleColor(3);
            if (minute % 30 != 25)
                ImGui.SameLine();
        }
    }

    private bool DrawMacro(Plugin plugin, ShoutMacro macro, long now)
    {
        bool removed = false;
        float width = Math.Max(40f, ImGui.GetContentRegionAvail().X);
        Vector2 origin = ImGui.GetCursorScreenPos();
        ImGui.BeginGroup();
        ImGui.PushID(macro.Id);
        if (macro.Dj)
        {
            ImGui.TextColored(Ui.Purple, macro.Name);
        }
        else
        {
            string title = macro.Name;
            ImGui.SetNextItemWidth(220);
            if (ImGui.InputText("##title", ref title, 48))
            {
                macro.Name = title;
                plugin.Config.Save();
            }
        }

        ImGui.SameLine();
        Channel(plugin, macro, shout: true);
        ImGui.SameLine();
        Channel(plugin, macro, shout: false);
        ImGui.SameLine();
        string prefixLabel = string.IsNullOrEmpty(macro.Prefix) ? "Symbols" : $"Symbols {macro.Prefix}";
        if (ImGui.Button(prefixLabel))
            plugin.OpenSymbols(symbol => plugin.SetPrefix(macro.Id, symbol));

        ImGui.SameLine();
        if (ImGui.Button(macro.Dj ? "Preview" : "Edit"))
            ImGui.OpenPopup("editor");
        ImGui.SetNextWindowSize(new Vector2(640, macro.Dj ? 140 : 220), ImGuiCond.Appearing);
        bool editorOpen = true;
        if (ImGui.BeginPopupModal("editor", ref editorOpen))
        {
            float close = ImGui.GetFrameHeightWithSpacing();
            Vector2 body = new(ImGui.GetContentRegionAvail().X, Math.Max(48f, ImGui.GetContentRegionAvail().Y - close));
            if (macro.Dj)
            {
                string preview = string.Join("\n", EventDefaults.Lines(macro, plugin.Config.Djs, now));
                ImGui.InputTextMultiline("##preview", ref preview, 800, body, ImGuiInputTextFlags.ReadOnly);
            }
            else
            {
                string text = macro.Text;
                if (ImGui.InputTextMultiline("##body", ref text, 800, body))
                {
                    macro.Text = text;
                    plugin.Config.Save();
                }
            }

            if (ImGui.Button("Close") || !editorOpen)
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        PushGo();
        if (ImGui.Button("Push"))
            Push(plugin, macro, now);
        ImGui.PopStyleColor(3);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90);
        int minutes = macro.IntervalMinutes;
        if (ImGui.InputInt("Interval min", ref minutes))
        {
            macro.IntervalMinutes = Math.Clamp(minutes, 0, 240);
            macro.NextPushUnix = macro.IntervalMinutes == 0 ? 0 : now + (macro.IntervalMinutes * 60L);
            plugin.Config.Save();
        }

        if (!EventDefaults.BuiltIn(macro.Id))
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
            {
                plugin.Config.Macros.Remove(macro);
                plugin.Config.Save();
                removed = true;
            }
        }

        ImGui.Text($"Last pushed  {When(macro.LastPushedUnix)}  {Ago(macro.LastPushedUnix, now)}");
        ImGui.SameLine();
        ImGui.TextColored(Ui.Muted, "|");
        ImGui.SameLine();
        ImGui.Text($"Next push  {When(macro.NextPushUnix)}  {Until(macro.NextPushUnix, now)}");
        ImGui.PopID();
        ImGui.EndGroup();
        Vector2 pad = new(6, 4);
        ImGui.GetWindowDrawList().AddRect(
            origin - pad,
            new Vector2(origin.X + width, ImGui.GetItemRectMax().Y) + pad,
            ImGui.GetColorU32(new Vector4(0.56f, 0.56f, 0.64f, 0.55f)),
            5f);
        ImGui.Dummy(new Vector2(0, 8));
        return removed;
    }

    private static void Channel(Plugin plugin, ShoutMacro macro, bool shout)
    {
        bool selected = shout ? !macro.Yell : macro.Yell;
        if (selected)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.36f, 0.28f, 0.55f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.46f, 0.36f, 0.68f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.56f, 0.44f, 0.78f, 1f));
        }

        if (ImGui.Button(shout ? "Shout" : "Yell") && !selected)
        {
            macro.Yell = !shout;
            macro.ChannelChosen = true;
            plugin.Config.Save();
        }

        if (selected)
            ImGui.PopStyleColor(3);
    }

    private void Push(Plugin plugin, ShoutMacro macro, long now)
    {
        IReadOnlyList<string> lines = EventDefaults.Lines(macro, plugin.Config.Djs, now);
        if (!ChatMacro.Run(lines))
        {
            _note = $"Could not push {macro.Name}.";
            return;
        }

        _note = null;
        macro.LastPushedUnix = now;
        macro.NextPushUnix = macro.IntervalMinutes == 0 ? 0 : now + (macro.IntervalMinutes * 60L);
        plugin.Config.Save();
        plugin.Client.NoteLocal($"Pushed {macro.Name}");
    }

    public static void PushDue(Plugin plugin)
    {
        if (plugin.Client.Snapshot == null)
            return;
        StaffRole role = plugin.Client.Snapshot.Member(plugin.Client.SelfId ?? "")?.Role ?? StaffRole.None;
        if (role != StaffRole.Director)
            return;

        long now = ServerClock.Unix();
        bool changed = false;
        foreach (ShoutMacro macro in plugin.Config.Macros)
        {
            if (macro.IntervalMinutes <= 0 || macro.NextPushUnix <= 0 || now < macro.NextPushUnix)
                continue;

            IReadOnlyList<string> lines = EventDefaults.Lines(macro, plugin.Config.Djs, now);
            if (!ChatMacro.Run(lines))
            {
                macro.NextPushUnix = now + 30;
                changed = true;
                continue;
            }

            macro.LastPushedUnix = now;
            macro.NextPushUnix = now + (macro.IntervalMinutes * 60L);
            plugin.Client.NoteLocal($"Pushed {macro.Name}");
            changed = true;
        }

        if (changed)
            plugin.Config.Save();
    }

    private static void PushGo()
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.16f, 0.38f, 0.26f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.24f, 0.52f, 0.34f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Ui.Good);
    }

    private static void PushSelected()
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.36f, 0.28f, 0.55f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.46f, 0.36f, 0.68f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.56f, 0.44f, 0.78f, 1f));
    }

    private static DateTime Utc(long unix) =>
        DateTimeOffset.FromUnixTimeSeconds(Math.Max(0, unix)).UtcDateTime;

    private static long Compose(int year, int month, int day, int hour, int minute) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    private static string When(long unix) => unix <= 0 ? "—" : Utc(unix).ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Ago(long unix, long now)
    {
        if (unix <= 0)
            return "never";
        return $"{Span(Math.Max(0, now - unix))} ago";
    }

    private static string Until(long unix, long now)
    {
        if (unix <= 0)
            return "—";
        long delta = unix - now;
        return delta <= 0 ? "due" : $"in {Span(delta)}";
    }

    private static string Span(long seconds)
    {
        long hours = seconds / 3600;
        long minutes = seconds % 3600 / 60;
        long rest = seconds % 60;
        return hours > 0 ? $"{hours}h {minutes:00}m {rest:00}s" : $"{minutes}m {rest:00}s";
    }
}
