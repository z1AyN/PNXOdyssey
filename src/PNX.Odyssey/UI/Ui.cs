using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Pnx.Odyssey.Core;
using Pnx.Odyssey.Services;

namespace Pnx.Odyssey.UI;

internal static class Ui
{
    public static readonly Vector4 Purple = new(0.678f, 0.541f, 0.961f, 1f);
    public static readonly Vector4 Gold = new(0.93f, 0.78f, 0.46f, 1f);
    public static readonly Vector4 Yellow = new(1f, 0.914f, 0.478f, 1f);
    public static readonly Vector4 Muted = new(0.561f, 0.561f, 0.561f, 1f);
    public static readonly Vector4 Amber = new(1f, 0.702f, 0.400f, 1f);
    public static readonly Vector4 Good = new(0.486f, 0.839f, 0.541f, 1f);
    public static readonly Vector4 Danger = new(0.831f, 0.267f, 0.267f, 1f);
    public static readonly Vector4 Text = new(0.94f, 0.93f, 0.97f, 1f);

    private static readonly Vector4 HeaderTop = new(0.078f, 0.051f, 0.149f, 1f);
    private static readonly Vector4 HeaderBottom = new(0.122f, 0.078f, 0.200f, 1f);

    private const int ColorCount = 21;
    private const int VarCount = 9;

    public static void PushFrame()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 6));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6, 3));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6, 4));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(6, 4));
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(6, 3));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 4f);

        ImGui.PushStyleColor(ImGuiCol.Text, Text);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.055f, 0.043f, 0.086f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.07f, 0.055f, 0.11f, 0.94f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.45f, 0.38f, 0.62f, 0.45f));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, HeaderTop);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, HeaderBottom);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.196f, 0.196f, 0.196f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.32f, 0.26f, 0.42f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.576f, 0.459f, 0.820f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.09f, 0.08f, 0.12f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.14f, 0.12f, 0.20f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.58f, 0.46f, 0.82f, 0.45f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.68f, 0.54f, 0.96f, 0.55f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.75f, 0.62f, 1f, 0.70f));
        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, new Vector4(0.12f, 0.09f, 0.18f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, new Vector4(0.68f, 0.54f, 0.96f, 0.05f));
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.68f, 0.54f, 0.96f, 0.35f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, new Vector4(0.04f, 0.03f, 0.06f, 0.5f));
        ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(0.12f, 0.09f, 0.18f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(0.40f, 0.30f, 0.58f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(0.576f, 0.459f, 0.820f, 1f));
    }

    public static void PopFrame()
    {
        ImGui.PopStyleColor(ColorCount);
        ImGui.PopStyleVar(VarCount);
    }

    public static void Section(string title)
    {
        ImGui.Dummy(new Vector2(0, 4));
        ImGui.TextColored(Purple, title);
        ImGui.Separator();
        ImGui.Dummy(new Vector2(0, 2));
    }

    public static void Hint(string text) => ImGui.TextColored(Muted, text);

    public static void ThreadMark(float size = 22f)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        Vector2 center = origin + new Vector2(size * 0.5f, size * 0.5f);
        uint color = ImGui.GetColorU32(new Vector4(0.90f, 0.82f, 0.58f, 1f));
        float radius = size * 0.22f;
        draw.AddCircleFilled(center + new Vector2(0, -size * 0.28f), radius, color);
        draw.AddRectFilled(
            center + new Vector2(-radius * 0.45f, -size * 0.22f),
            center + new Vector2(radius * 0.45f, size * 0.22f),
            color);
        draw.AddCircleFilled(center + new Vector2(0, size * 0.28f), radius, color);
        ImGui.Dummy(new Vector2(size, size));
    }

    public static void RunMark(float size = 22f)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        Vector2 center = origin + new Vector2(size * 0.5f, size * 0.5f);
        uint color = ImGui.GetColorU32(new Vector4(0.68f, 0.54f, 0.96f, 1f));
        float radius = size * 0.32f;
        draw.AddCircle(center, radius, color, 16, 1.8f);
        draw.AddTriangleFilled(
            center + new Vector2(radius * 0.15f, -radius * 0.95f),
            center + new Vector2(radius * 0.95f, -radius * 0.1f),
            center + new Vector2(radius * 0.1f, -radius * 0.1f),
            color);
        ImGui.Dummy(new Vector2(size, size));
    }

    public static void Space() => ImGui.Dummy(new Vector2(0, 2));

    public static TrialPaint TrialColor(TrialAspect aspect) => aspect switch
    {
        TrialAspect.Strength => new(new Vector4(0.800f, 0f, 0f, 1f), Vector4.One),
        TrialAspect.Harmony => new(new Vector4(0.659f, 0.271f, 1f, 1f), Vector4.One),
        TrialAspect.Fear => new(new Vector4(0f, 0.741f, 0.063f, 1f), new Vector4(0.06f, 0.06f, 0.05f, 1f)),
        _ => new(new Vector4(0.980f, 0.847f, 0.322f, 1f), new Vector4(0.10f, 0.07f, 0.02f, 1f)),
    };

    public readonly record struct TrialPaint(Vector4 Fill, Vector4 Ink);

    public static void Sky(Action body)
    {
        Vector2 start = ImGui.GetCursorScreenPos();
        float width = Math.Max(40f, ImGui.GetContentRegionAvail().X);
        float height = ImGui.GetFrameHeight() + 8f;
        var end = new Vector2(start.X + width, start.Y + height);
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        draw.AddRectFilledMultiColor(
            start,
            end,
            ImGui.GetColorU32(HeaderTop),
            ImGui.GetColorU32(HeaderTop),
            ImGui.GetColorU32(HeaderBottom),
            ImGui.GetColorU32(HeaderBottom));
        DrawStars(draw, start, end);
        ImGui.SetCursorScreenPos(start + new Vector2(6, 4));
        body();
        ImGui.SetCursorScreenPos(new Vector2(start.X, end.Y + 4));
    }

    public static void Group(Action body)
    {
        ImGui.BeginGroup();
        body();
        ImGui.EndGroup();
        Vector2 pad = new(4, 3);
        ImGui.GetWindowDrawList().AddRect(
            ImGui.GetItemRectMin() - pad,
            ImGui.GetItemRectMax() + pad,
            ImGui.GetColorU32(new Vector4(0.56f, 0.56f, 0.64f, 0.55f)),
            5f);
        ImGui.Dummy(new Vector2(0, 2));
    }

    public static void Sync(LinkState state, string status)
    {
        (Vector4 color, string label) = state switch
        {
            LinkState.Connected => (Good, "Synced"),
            LinkState.Syncing => (Yellow, "Syncing"),
            _ => (Muted, "Offline"),
        };

        ImGui.TextColored(color, "●");
        ImGui.SameLine(0, 6);
        ImGui.TextColored(color, label);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(status);
    }

    public static void Banner(string? text, Vector4 color)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        Space();
        ImGui.TextColored(color, text);
    }

    public static bool Foreign(OdysseyClient client, string participantId, out string holder)
    {
        if (client.LockedByOther(EditScope.Threads(participantId), out holder)) return true;
        if (client.LockedByOther(EditScope.Register(participantId), out holder)) return true;
        if (client.LockedByOther(EditScope.Trial(participantId), out holder)) return true;
        holder = "";
        return false;
    }

    public static void Editing(string holder)
    {
        Space();
        ImGui.TextColored(Amber, $"{holder} is editing this.");
    }

    private static void DrawStars(ImDrawListPtr draw, Vector2 start, Vector2 end)
    {
        float width = end.X - start.X;
        float height = end.Y - start.Y;
        float time = (float)ImGui.GetTime();
        uint still = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.85f));
        uint shooting = ImGui.GetColorU32(new Vector4(0.40f, 0.80f, 1f, 0.95f));
        ReadOnlySpan<(float X, float Y, float Size, bool Drift)> stars =
        [
            (0.06f, 0.28f, 1.1f, false),
            (0.14f, 0.72f, 1.5f, false),
            (0.27f, 0.40f, 1.0f, false),
            (0.41f, 0.22f, 1.3f, false),
            (0.55f, 0.68f, 1.1f, false),
            (0.73f, 0.30f, 1.4f, false),
            (0.88f, 0.62f, 1.0f, false),
            (0.18f, 0.48f, 1.3f, true),
            (0.62f, 0.36f, 1.2f, true),
        ];

        foreach ((float x, float y, float size, bool drift) in stars)
        {
            float px = x;
            float py = y;
            if (drift)
            {
                float travel = (time * 0.045f + x) % 1f;
                px = travel;
                py = Math.Clamp(y + ((travel - 0.5f) * 0.22f), 0.08f, 0.92f);
                for (int step = 1; step <= 4; step++)
                {
                    float trail = travel - (step * 0.025f);
                    if (trail < 0f)
                        continue;
                    float alpha = 0.45f - (step * 0.1f);
                    draw.AddCircleFilled(
                        start + new Vector2(width * trail, height * py),
                        size * 0.7f,
                        ImGui.GetColorU32(new Vector4(0.40f, 0.80f, 1f, alpha)));
                }
            }

            draw.AddCircleFilled(start + new Vector2(width * px, height * py), size, drift ? shooting : still);
        }
    }

    private static IFontHandle? _nameFont;
    private static IFontAtlas? _atlas;
    private static readonly Dictionary<int, IFontHandle> _chatFonts = [];
    private static readonly Dictionary<int, IFontHandle> _italicFonts = [];

    public static void LoadNameFont(IFontAtlas atlas)
    {
        _atlas = atlas;
        _nameFont ??= atlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Axis36));
    }

    public static void LoadItalicFont(IFontAtlas atlas) => _atlas = atlas;

    public static void ReleaseNameFont()
    {
        _nameFont?.Dispose();
        _nameFont = null;
        foreach (IFontHandle font in _chatFonts.Values)
            font.Dispose();
        foreach (IFontHandle font in _italicFonts.Values)
            font.Dispose();
        _chatFonts.Clear();
        _italicFonts.Clear();
    }

    public static IDisposable PushPixels(int pixels, bool italic)
    {
        IFontHandle? handle = ChatFont(pixels, italic) ?? ChatFont(pixels, false);
        return handle?.Push() ?? NoFont.Instance;
    }

    private static IFontHandle? ChatFont(int pixels, bool italic)
    {
        if (_atlas == null)
            return null;

        int size = Math.Clamp(pixels, 12, 24);
        Dictionary<int, IFontHandle> fonts = italic ? _italicFonts : _chatFonts;
        if (fonts.TryGetValue(size, out IFontHandle? existing))
            return existing;

        string file = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            italic ? "ariali.ttf" : "arial.ttf");
        if (!File.Exists(file))
            return null;

        IFontHandle created = _atlas.NewDelegateFontHandle(e => e.OnPreBuild(toolkit =>
        {
            SafeFontConfig config = new() { SizePx = size };
            toolkit.AddFontFromFile(file, in config);
        }));
        fonts[size] = created;
        return created;
    }

    private sealed class NoFont : IDisposable
    {
        public static readonly NoFont Instance = new();
        public void Dispose()
        {
        }
    }

    public static void NameLine(string text)
    {
        IDisposable? pushed = _nameFont?.Push();
        float width = ImGui.CalcTextSize(text).X;
        float spare = ImGui.GetContentRegionAvail().X - width;
        if (spare > 1f)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (spare * 0.5f));
        ImGui.TextUnformatted(text);
        pushed?.Dispose();
    }

    public static float ButtonWidth(string label)
    {
        string visible = label;
        int hash = label.IndexOf("##", StringComparison.Ordinal);
        if (hash >= 0)
            visible = label[..hash];
        Vector2 pad = ImGui.GetStyle().FramePadding;
        return ImGui.CalcTextSize(visible).X + (pad.X * 2f);
    }

    /// <summary>Continue on the previous line when <paramref name="label"/> fits; otherwise start a new row.</summary>
    public static void WrapSameLine(string label, float reserve = 0f)
    {
        float width = ButtonWidth(label);
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float right = ImGui.GetWindowPos().X + ImGui.GetContentRegionMax().X;
        if (ImGui.GetItemRectMax().X + spacing + width + reserve < right)
            ImGui.SameLine();
    }

    public static Vector4 DefaultMacroColor { get; } = new(0.196f, 0.196f, 0.196f, 1f);

    public static Vector4 ParseColor(string? hex, Vector4 fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return fallback;
        string value = hex.Trim();
        if (value.StartsWith('#') || value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value.StartsWith('#') ? value[1..] : value[2..];
        if (value.Length is not (6 or 8))
            return fallback;
        if (!uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out uint packed))
            return fallback;
        if (value.Length == 6)
            packed = (packed << 8) | 0xFFu;
        return new Vector4(
            ((packed >> 24) & 0xFF) / 255f,
            ((packed >> 16) & 0xFF) / 255f,
            ((packed >> 8) & 0xFF) / 255f,
            (packed & 0xFF) / 255f);
    }

    public static string FormatColor(Vector4 color)
    {
        int r = Math.Clamp((int)MathF.Round(color.X * 255f), 0, 255);
        int g = Math.Clamp((int)MathF.Round(color.Y * 255f), 0, 255);
        int b = Math.Clamp((int)MathF.Round(color.Z * 255f), 0, 255);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    public static bool ColoredButton(string label, Vector4 fill, Vector2? size = null)
    {
        Vector4 hover = fill with
        {
            X = Math.Min(1f, fill.X + 0.08f),
            Y = Math.Min(1f, fill.Y + 0.08f),
            Z = Math.Min(1f, fill.Z + 0.08f),
        };
        Vector4 active = fill with
        {
            X = Math.Min(1f, fill.X + 0.14f),
            Y = Math.Min(1f, fill.Y + 0.14f),
            Z = Math.Min(1f, fill.Z + 0.14f),
        };
        float luma = (fill.X * 0.2126f) + (fill.Y * 0.7152f) + (fill.Z * 0.0722f);
        Vector4 ink = luma > 0.55f ? new Vector4(0.08f, 0.07f, 0.10f, 1f) : Text;
        ImGui.PushStyleColor(ImGuiCol.Button, fill);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, active);
        ImGui.PushStyleColor(ImGuiCol.Text, ink);
        bool pressed = size is { } fixedSize ? ImGui.Button(label, fixedSize) : ImGui.Button(label);
        ImGui.PopStyleColor(4);
        return pressed;
    }

    public static bool MacroColorPopup(string popupId, ref Vector4 color)
    {
        if (!ImGui.BeginPopup(popupId))
            return false;
        bool changed = ImGui.ColorEdit4(
            "##macro-color",
            ref color,
            ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.PickerHueWheel);
        if (ImGui.Button("Reset"))
        {
            color = DefaultMacroColor;
            changed = true;
        }

        ImGui.EndPopup();
        return changed;
    }

    public static void DiceTable(
        string playerLabel,
        IReadOnlyList<int> playerRolls,
        string godLabel,
        IReadOnlyList<int> godRolls,
        Action playerAction,
        Action godAction)
    {
        if (!ImGui.BeginTable("##dice", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
            return;

        ImGui.TableSetupColumn("Side");
        ImGui.TableSetupColumn("Dice");
        ImGui.TableSetupColumn("Total");
        ImGui.TableSetupColumn(string.Empty);
        ImGui.TableHeadersRow();
        DiceTableRow(playerLabel, playerRolls, playerAction);
        DiceTableRow(godLabel, godRolls, godAction);
        ImGui.EndTable();
    }

    private static void DiceTableRow(string label, IReadOnlyList<int> rolls, Action action)
    {
        RollSet current = DiceGrouping.Latest(rolls);
        ImGui.TableNextRow();
        CenterText(label);
        ImGui.TableNextColumn();
        float diceWidth = (TrialRules.DicePerSet * DieSize) + ((TrialRules.DicePerSet - 1) * 8f);
        Center(diceWidth);
        for (int index = 0; index < TrialRules.DicePerSet; index++)
        {
            if (index > 0)
                ImGui.SameLine(0, 8);
            int? face = index < current.Faces.Count ? current.Faces[index] : null;
            Die(face);
        }

        int total = current.IsComplete ? current.Total : current.Faces.Sum();
        CenterText(current.Faces.Count == 0 ? "—" : total.ToString());
        ImGui.TableNextColumn();
        action();
    }

    private const float DieSize = 34f;

    private static void CenterText(string text)
    {
        ImGui.TableNextColumn();
        float width = ImGui.CalcTextSize(text).X;
        Center(width);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(text);
    }

    private static void Center(float itemWidth)
    {
        float spare = ImGui.GetContentRegionAvail().X - itemWidth;
        if (spare > 1f)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (spare * 0.5f));
    }

    public static void DiceRow(string title, IReadOnlyList<int> rolls)
    {
        RollSet current = DiceGrouping.Latest(rolls);
        ImGui.AlignTextToFramePadding();
        ImGui.Text(title);
        ImGui.SameLine(0, 12);
        for (int index = 0; index < TrialRules.DicePerSet; index++)
        {
            int? face = index < current.Faces.Count ? current.Faces[index] : null;
            Die(face);
            ImGui.SameLine(0, 8);
        }

        int total = current.IsComplete ? current.Total : current.Faces.Sum();
        ImGui.AlignTextToFramePadding();
        ImGui.Text(current.Faces.Count == 0 ? "Total  —" : $"Total  {total}");
    }

    public static void Die(int? value, float size = 34f)
    {
        Vector2 pos = ImGui.GetCursorScreenPos();
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        Vector2 max = pos + new Vector2(size, size);
        uint face = ImGui.GetColorU32(new Vector4(0.96f, 0.94f, 0.88f, 1f));
        uint edge = ImGui.GetColorU32(new Vector4(0.45f, 0.34f, 0.14f, 1f));
        uint ink = ImGui.GetColorU32(new Vector4(0.14f, 0.11f, 0.08f, 1f));
        if (value == null)
        {
            face = ImGui.GetColorU32(new Vector4(0.16f, 0.14f, 0.12f, 1f));
            edge = ImGui.GetColorU32(new Vector4(0.34f, 0.28f, 0.16f, 0.8f));
        }

        draw.AddRectFilled(pos + new Vector2(2, 3), max + new Vector2(2, 3), ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)), 7f);
        draw.AddRectFilled(pos, max, face, 7f);
        draw.AddRect(pos, max, edge, 7f, ImDrawFlags.None, 1.5f);
        if (value is int number)
            DrawPips(draw, pos, size, number, ink);
        ImGui.Dummy(new Vector2(size, size));
    }

    private static void DrawPips(ImDrawListPtr draw, Vector2 origin, float size, int number, uint ink)
    {
        float radius = size * 0.08f;
        bool[] pips = number switch
        {
            1 => [false, false, false, false, true, false, false, false, false],
            2 => [true, false, false, false, false, false, false, false, true],
            3 => [true, false, false, false, true, false, false, false, true],
            4 => [true, false, true, false, false, false, true, false, true],
            5 => [true, false, true, false, true, false, true, false, true],
            7 => [true, false, true, true, true, true, true, false, true],
            8 => [true, true, true, true, false, true, true, true, true],
            _ => [true, false, true, true, false, true, true, false, true],
        };
        if (number > 8)
        {
            string text = number.ToString();
            Vector2 textSize = ImGui.CalcTextSize(text);
            draw.AddText(origin + new Vector2((size - textSize.X) / 2f, (size - textSize.Y) / 2f), ink, text);
            return;
        }
        for (int index = 0; index < 9; index++)
        {
            if (!pips[index])
                continue;
            float x = origin.X + size * (0.26f + (index % 3) * 0.24f);
            float y = origin.Y + size * (0.26f + (index / 3) * 0.24f);
            draw.AddCircleFilled(new Vector2(x, y), radius, ink);
        }
    }
}

internal static class SessionChrome
{
    public static StaffRole RoleOf(OdysseyClient client, SessionSnapshot snapshot) =>
        snapshot.Member(client.SelfId ?? "")?.Role ?? StaffRole.None;

    public static void Header(Plugin plugin, SessionSnapshot snapshot)
    {
        OdysseyClient client = plugin.Client;
        Ui.Sky(() =>
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(Ui.Purple, snapshot.Name);
            ImGui.SameLine();
            ImGui.TextColored(Ui.Muted, SessionTime.Format(SessionTime.Elapsed(snapshot.CreatedAt)));
            ImGui.SameLine();
            ImGui.Text("Role");
            ImGui.SameLine();
            RoleCombo(client, snapshot);
            ImGui.SameLine();
            if (ImGui.Button("Leave"))
                client.Leave();
        });
    }

    public static void Strip(SessionSnapshot snapshot)
    {
        Ui.Space();
        IEnumerable<string> seated = snapshot.Members
            .Where(member => member.Connected)
            .Select(member => $"{member.Name}  ·  {StaffText.Label(member.Role)}");
        string line = string.Join("      ", seated);
        if (line.Length == 0)
            Ui.Hint("No one is connected.");
        else
            ImGui.Text(line);
    }

    public static void Members(SessionSnapshot snapshot)
    {
        Ui.Section("Lobby");
        if (snapshot.Members.Count == 0)
        {
            Ui.Hint("Nobody is connected.");
            return;
        }

        foreach (SessionMember member in snapshot.Members)
        {
            ImGui.PushID(member.Id);
            ImGui.Text($"{member.Name}   ·   {member.World}   ·   {StaffText.Label(member.Role)}");
            ImGui.SameLine();
            ImGui.TextColored(member.Connected ? Ui.Good : Ui.Muted, member.Connected ? "live" : "away");
            ImGui.PopID();
            Ui.Space();
        }
    }

    private static void RoleCombo(OdysseyClient client, SessionSnapshot snapshot)
    {
        StaffRole current = RoleOf(client, snapshot);
        ImGui.SetNextItemWidth(190);
        if (!ImGui.BeginCombo("##role", StaffText.Label(current)))
            return;

        foreach (StaffRole role in Enum.GetValues<StaffRole>())
        {
            SessionMember? taken = StaffText.IsExclusive(role)
                ? snapshot.Members.FirstOrDefault(member => member.Connected && member.Role == role && member.Id != client.SelfId)
                : null;
            string label = taken == null
                ? StaffText.Label(role)
                : $"{StaffText.Label(role)}   ·   {taken.Name}";
            ImGui.BeginDisabled(taken != null);
            if (ImGui.Selectable(label, role == current) && taken == null)
                client.SetRole(role);
            ImGui.EndDisabled();
        }

        ImGui.EndCombo();
    }

    public static void Footer(Plugin plugin)
    {
        Ui.Sky(() =>
        {
            if (ImGui.Button("Settings"))
                plugin.OpenConfig();
            ImGui.SameLine();
            bool chatOpen = plugin.ChatOpen;
            if (chatOpen)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.36f, 0.28f, 0.55f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.46f, 0.36f, 0.68f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.56f, 0.44f, 0.78f, 1f));
            }

            if (ImGui.Button("Chatlog"))
                plugin.ToggleChat();
            if (chatOpen)
                ImGui.PopStyleColor(3);

            Version versionInfo = typeof(Plugin).Assembly.GetName().Version ?? new Version(0, 1, 0);
            string label = $"v{versionInfo.Major}.{versionInfo.Minor}.{versionInfo.Build}";
            float syncWidth = 78f;
            float versionWidth = ImGui.CalcTextSize(label).X;
            float rightBlock = syncWidth + versionWidth + 8f;
            float right = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - rightBlock;
            string? notice = plugin.Client.Banner;
            if (!string.IsNullOrWhiteSpace(notice))
            {
                float textWidth = ImGui.CalcTextSize(notice).X;
                float centre = (ImGui.GetCursorPosX() + right) * 0.5f - textWidth * 0.5f;
                if (centre > ImGui.GetCursorPosX() + 8f)
                    ImGui.SameLine(centre);
                else
                    ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(Ui.Amber, notice);
            }

            if (right > ImGui.GetCursorPosX())
                ImGui.SameLine(right);
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(Ui.Muted, label);
            ImGui.SameLine();
            Ui.Sync(plugin.Client.State, plugin.Client.Status);
        });
    }
}
