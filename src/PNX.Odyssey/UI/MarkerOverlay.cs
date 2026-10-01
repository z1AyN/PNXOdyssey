using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.UI;

internal sealed class MarkerOverlay : Window
{
    private readonly Plugin _plugin;
    private readonly Dictionary<string, double> _shownAt = new(StringComparer.OrdinalIgnoreCase);
    private string _lastTarget = "\0";

    public MarkerOverlay(Plugin plugin) : base("PNX Odyssey Mark##mark",
        ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoInputs
        | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.NoBringToFrontOnFocus
        | ImGuiWindowFlags.NoFocusOnAppearing)
    {
        _plugin = plugin;
        IsOpen = true;
        RespectCloseHotkey = false;
        ForceMainWindow = true;
    }

    public override bool DrawConditions() =>
        _plugin.HasMarks || (_plugin.Client.Snapshot?.Participants.Count ?? 0) > 0;

    public override void PreDraw()
    {
        Vector2 origin = ImGui.GetMainViewport().Pos;
        Vector2 size = ImGui.GetMainViewport().Size;
        ImGui.SetNextWindowPos(origin);
        ImGui.SetNextWindowSize(size);
    }

    public override void Draw()
    {
        Vector2 origin = ImGui.GetMainViewport().Pos;
        ImDrawListPtr draw = ImGui.GetBackgroundDrawList();
        Vector3? from = _plugin.LocalChest();
        if (from != null)
        {
            foreach (Vector3 target in _plugin.MarkedPositions())
                DrawLine(draw, origin, from.Value, target);
        }

        Vector2 view = ImGui.GetMainViewport().Size;
        NoteTarget();
        var visible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach ((Participant player, Vector3 head) in _plugin.NearbyRegistered())
        {
            visible.Add(player.Id);
            float fade = LabelOpacity(player);
            if (fade <= 0.01f)
                continue;
            if (!_plugin.Project(head, out Vector2 screen))
                continue;
            if (screen.X < 0 || screen.Y < 0 || screen.X > view.X || screen.Y > view.Y)
                continue;
            DrawProgress(draw, screen + origin, player, fade);
        }

        List<string> gone = _shownAt.Keys.Where(id => !visible.Contains(id)).ToList();
        foreach (string id in gone)
            _shownAt.Remove(id);
    }

    private void NoteTarget()
    {
        if (!_plugin.Config.OverlayFade)
        {
            _lastTarget = "\0";
            return;
        }

        string target = TargetedId() ?? "";
        if (string.Equals(target, _lastTarget, StringComparison.OrdinalIgnoreCase))
            return;

        _lastTarget = target;
        if (target.Length > 0)
            _shownAt[target] = ImGui.GetTime();
    }

    private float LabelOpacity(Participant player)
    {
        Configuration config = _plugin.Config;
        double now = ImGui.GetTime();
        string? targetedId = TargetedId();
        bool aimed = targetedId != null && string.Equals(targetedId, player.Id, StringComparison.OrdinalIgnoreCase);
        if (config.OverlayFade && aimed && _shownAt.TryGetValue(player.Id, out double aimedAt))
            return AgeFade(now, aimedAt, config.OverlayFadeSeconds);

        if (!config.OverlayEnabled)
            return 0f;
        if (!config.OverlayFade)
            return 1f;
        if (!_shownAt.TryGetValue(player.Id, out double started))
        {
            started = now;
            _shownAt[player.Id] = started;
        }

        return AgeFade(now, started, config.OverlayFadeSeconds);
    }

    private string? TargetedId()
    {
        SessionSnapshot? snapshot = _plugin.Client.Snapshot;
        if (snapshot == null || !_plugin.TryTarget(out PartyPresence presence))
            return null;
        return PartyMatcher.Find(snapshot, presence.Name, presence.World)?.Id;
    }

    private static float AgeFade(double now, double started, float hold)
    {
        float age = (float)(now - started);
        if (age <= hold)
            return 1f;
        float fade = (age - hold) / 0.8f;
        return fade >= 1f ? 0f : 1f - fade;
    }

    private void DrawLine(ImDrawListPtr draw, Vector2 origin, Vector3 from, Vector3 to)
    {
        var points = new List<Vector2>(17);
        for (int step = 0; step <= 16; step++)
        {
            Vector3 world = Vector3.Lerp(from, to, step / 16f);
            if (!_plugin.Project(world, out Vector2 screen))
                continue;
            points.Add(screen + origin);
        }

        if (points.Count < 2)
            return;

        Span<Vector2> span = CollectionsMarshalSpan(points);
        draw.AddPolyline(ref span[0], points.Count, ImGui.GetColorU32(new Vector4(1f, 0.78f, 0.28f, 0.28f)), ImDrawFlags.None, 10f);
        draw.AddPolyline(ref span[0], points.Count, ImGui.GetColorU32(new Vector4(1f, 0.86f, 0.42f, 0.7f)), ImDrawFlags.None, 4f);
        draw.AddPolyline(ref span[0], points.Count, ImGui.GetColorU32(new Vector4(1f, 0.95f, 0.72f, 1f)), ImDrawFlags.None, 1.6f);
    }

    private static Span<Vector2> CollectionsMarshalSpan(List<Vector2> points) => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(points);

    private static readonly TrialBadge[] Trials =
    [
        new(TrialAspect.Strength, new(0.80f, 0.00f, 0.00f, 1f), Badge.Spear),
        new(TrialAspect.Harmony, new(0.66f, 0.27f, 1.00f, 1f), Badge.Crown),
        new(TrialAspect.Fear, new(0.00f, 0.74f, 0.06f, 1f), Badge.Wave),
        new(TrialAspect.Power, new(0.98f, 0.85f, 0.32f, 1f), Badge.Bolt),
    ];

    private readonly record struct TrialBadge(TrialAspect Aspect, Vector4 Color, Badge Shape);

    private enum Badge
    {
        Spear,
        Crown,
        Wave,
        Bolt,
    }

    private void DrawProgress(ImDrawListPtr draw, Vector2 anchor, Participant player, float fade)
    {
        float scale = Math.Clamp(_plugin.Config.OverlayScale, 0.6f, 2.2f);
        float opacity = Math.Clamp(_plugin.Config.OverlayOpacity, 0.15f, 1f) * fade;
        float icon = 16f * scale;
        float gap = 6f * scale;
        float pad = 6f * scale;
        string threads = player.Threads.ToString();
        string run = Math.Max(player.Level, 1).ToString();
        float fontSize = ImGui.GetFontSize() * scale;
        float threadWidth = ImGui.CalcTextSize(threads).X * scale;
        float runWidth = ImGui.CalcTextSize(run).X * scale;
        float iconsWidth = (Trials.Length * icon) + ((Trials.Length - 1) * gap);
        float statsWidth = icon + gap + threadWidth + (14f * scale) + icon + gap + runWidth;
        float width = Math.Max(iconsWidth, statsWidth) + (pad * 2f);
        float row = Math.Max(icon, fontSize);
        float height = (pad * 2f) + row + (4f * scale) + row;
        var min = new Vector2(anchor.X - (width * 0.5f), anchor.Y - height);
        draw.AddRectFilled(min, min + new Vector2(width, height), ImGui.GetColorU32(new Vector4(0.03f, 0.02f, 0.05f, 0.78f * opacity)), 5f * scale);

        float iconX = min.X + ((width - iconsWidth) * 0.5f);
        float iconY = min.Y + pad;
        foreach (TrialBadge trial in Trials)
        {
            bool lit = TrialRules.Victor(player, trial.Aspect) != null;
            DrawBadge(draw, new Vector2(iconX, iconY + ((row - icon) * 0.5f)), icon, trial, lit, opacity);
            iconX += icon + gap;
        }

        float statsX = min.X + ((width - statsWidth) * 0.5f);
        float mid = min.Y + pad + row + (4f * scale) + (row * 0.5f);
        uint thread = Fade(new Vector4(0.90f, 0.82f, 0.58f, 1f), opacity);
        uint text = Fade(new Vector4(0.96f, 0.95f, 0.98f, 1f), opacity);
        uint ring = Fade(new Vector4(0.68f, 0.54f, 0.96f, 1f), opacity);
        Spool(draw, new Vector2(statsX + (icon * 0.5f), mid), icon, thread);
        statsX += icon + gap;
        Text(draw, new Vector2(statsX, mid), threads, text, fontSize);
        statsX += threadWidth + (14f * scale);
        Ring(draw, new Vector2(statsX + (icon * 0.5f), mid), icon * 0.42f, ring);
        statsX += icon + gap;
        Text(draw, new Vector2(statsX, mid), run, text, fontSize);
    }

    private static uint Fade(Vector4 color, float opacity) => ImGui.GetColorU32(color with { W = color.W * opacity });

    private static void Text(ImDrawListPtr draw, Vector2 leftCenter, string text, uint color, float fontSize)
    {
        Vector2 size = ImGui.CalcTextSize(text) * (fontSize / Math.Max(1f, ImGui.GetFontSize()));
        draw.AddText(ImGui.GetFont(), fontSize, new Vector2(leftCenter.X, leftCenter.Y - (size.Y * 0.5f)), color, text);
    }

    private static void Spool(ImDrawListPtr draw, Vector2 center, float size, uint color)
    {
        float radius = size * 0.22f;
        draw.AddCircleFilled(center + new Vector2(0, -size * 0.28f), radius, color);
        draw.AddRectFilled(
            center + new Vector2(-radius * 0.45f, -size * 0.22f),
            center + new Vector2(radius * 0.45f, size * 0.22f),
            color);
        draw.AddCircleFilled(center + new Vector2(0, size * 0.28f), radius, color);
    }

    private static void Ring(ImDrawListPtr draw, Vector2 center, float radius, uint color)
    {
        draw.AddCircle(center, radius, color, 16, 1.6f);
        draw.AddTriangleFilled(
            center + new Vector2(radius * 0.2f, -radius * 0.95f),
            center + new Vector2(radius * 0.95f, -radius * 0.15f),
            center + new Vector2(radius * 0.15f, -radius * 0.15f),
            color);
    }

    private static void DrawBadge(ImDrawListPtr draw, Vector2 min, float size, TrialBadge trial, bool lit, float opacity)
    {
        Vector4 color = trial.Color with { W = opacity * (lit ? 1f : 0.28f) };
        uint ink = ImGui.GetColorU32(color);
        Vector2 center = min + new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.42f;
        if (!lit)
            draw.AddCircle(center, radius, ink, 12, 1.2f);

        switch (trial.Shape)
        {
            case Badge.Spear:
                Triangle(draw, center, radius, up: true, ink, lit);
                break;
            case Badge.Crown:
                draw.AddTriangleFilled(center + new Vector2(-radius, radius * 0.4f), center + new Vector2(-radius * 0.35f, -radius), center + new Vector2(0, radius * 0.15f), ink);
                draw.AddTriangleFilled(center + new Vector2(-radius * 0.2f, radius * 0.2f), center, center + new Vector2(0, -radius), ink);
                draw.AddTriangleFilled(center + new Vector2(0, radius * 0.15f), center + new Vector2(radius * 0.35f, -radius), center + new Vector2(radius, radius * 0.4f), ink);
                break;
            case Badge.Wave:
                float y1 = center.Y - radius * 0.35f;
                float y2 = center.Y + radius * 0.35f;
                draw.AddLine(new Vector2(center.X - radius, y1), new Vector2(center.X + radius, y1), ink, 1.4f);
                draw.AddLine(new Vector2(center.X - radius, center.Y), new Vector2(center.X + radius, center.Y), ink, 1.4f);
                draw.AddLine(new Vector2(center.X - radius, y2), new Vector2(center.X + radius, y2), ink, 1.4f);
                break;
            default:
                Vector2[] bolt = BoltPoints(center, radius);
                draw.AddPolyline(ref bolt[0], 4, ink, ImDrawFlags.None, 1.8f);
                break;
        }
    }

    private static void Triangle(ImDrawListPtr draw, Vector2 center, float radius, bool up, uint color, bool filled)
    {
        Vector2 a = center + new Vector2(0, up ? -radius : radius);
        Vector2 b = center + new Vector2(-radius, up ? radius * 0.75f : -radius * 0.75f);
        Vector2 c = center + new Vector2(radius, up ? radius * 0.75f : -radius * 0.75f);
        if (filled)
            draw.AddTriangleFilled(a, b, c, color);
        else
            draw.AddTriangle(a, b, c, color, 1.3f);
    }

    private static Vector2[] BoltPoints(Vector2 center, float radius) =>
    [
        center + new Vector2(-radius * 0.2f, -radius),
        center + new Vector2(radius * 0.55f, -radius * 0.05f),
        center + new Vector2(-radius * 0.15f, radius * 0.05f),
        center + new Vector2(radius * 0.25f, radius),
    ];
}
