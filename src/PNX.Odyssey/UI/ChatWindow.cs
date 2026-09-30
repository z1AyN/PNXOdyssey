using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.UI;

internal sealed class ChatWindow : Window
{
    private static readonly (string Label, uint Id)[] Sounds =
    [
        ("Sound 1", 0x25),
        ("Sound 2", 0x26),
        ("Sound 3", 0x27),
        ("Sound 4", 0x28),
        ("Sound 5", 0x29),
        ("Sound 6", 0x2A),
        ("Sound 7", 0x2B),
        ("Sound 8", 0x2C),
        ("Sound 9", 0x2D),
        ("Sound 10", 0x2E),
        ("Sound 11", 0x2F),
        ("Sound 12", 0x30),
        ("Sound 13", 0x31),
        ("Sound 14", 0x32),
        ("Sound 15", 0x33),
        ("Sound 16", 0x34),
    ];

    private static readonly Vector4 Talk = new(0.22f, 0.48f, 0.28f, 1f);

    private readonly Plugin _plugin;
    private string _draft = "";
    private bool _heardReady;
    private int _heardChats;

    public ChatWindow(Plugin plugin) : base("PNX Odyssey Chat##chat")
    {
        _plugin = plugin;
        Size = new Vector2(760, 460);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw() => Ui.PushFrame();

    public override void PostDraw() => Ui.PopFrame();

    public override void Draw()
    {
        SessionSnapshot? snapshot = _plugin.Client.Snapshot;
        if (snapshot == null)
        {
            Ui.Hint("Join a session to use the lobby chat.");
            return;
        }

        float input = (ImGui.GetFrameHeightWithSpacing() * 1.15f) + ImGui.GetFrameHeightWithSpacing();
        ImGui.BeginChild("lobby-log", new Vector2(0, -input));
        int pixels = Math.Clamp(_plugin.Config.ChatTextSize, 12, 24);
        using (Ui.PushPixels(pixels, false))
        {
            IReadOnlyList<LobbyLine> lines = _plugin.Client.Lines;
            float timeSlot = ImGui.CalcTextSize("[00:00:00]").X + 10f;
            int chats = 0;
            foreach (LobbyLine line in lines)
            {
                if (line.Kind == LobbyKind.Chat)
                    chats++;
                DrawLine(line, timeSlot, pixels);
            }

            if (!_heardReady)
            {
                _heardReady = true;
                _heardChats = chats;
            }
            else if (chats > _heardChats)
            {
                Play(_plugin.Config.ChatSound);
                _heardChats = chats;
            }

            if (lines.Count == 0)
                Ui.Hint("Nothing in the lobby yet.");
        }

        ImGui.EndChild();

        int sound = Math.Clamp(_plugin.Config.ChatSound, 1, Sounds.Length);
        ImGui.SetNextItemWidth(160);
        if (ImGui.BeginCombo("Message sound", Sounds[sound - 1].Label))
        {
            for (int index = 0; index < Sounds.Length; index++)
            {
                if (!ImGui.Selectable(Sounds[index].Label, index + 1 == sound))
                    continue;
                _plugin.Config.ChatSound = index + 1;
                _plugin.Config.Save();
                Play(index + 1);
            }

            ImGui.EndCombo();
        }
        ImGui.SameLine();
        int textSize = Math.Clamp(_plugin.Config.ChatTextSize, 12, 24);
        ImGui.SetNextItemWidth(120);
        if (ImGui.SliderInt("Text size", ref textSize, 12, 24))
        {
            _plugin.Config.ChatTextSize = textSize;
            _plugin.Config.Save();
        }

        Vector2 pad = ImGui.GetStyle().FramePadding;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, pad + new Vector2(0f, ImGui.GetFrameHeight() * 0.075f));
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 78);
        bool send = ImGui.InputText("##say", ref _draft, 240, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        send |= ImGui.Button("Send");
        ImGui.PopStyleVar();
        if (!send)
            return;

        string text = _draft.Trim();
        if (text.Length == 0)
            return;
        _plugin.Client.Say(text);
        _draft = "";
    }

    private static void DrawLine(LobbyLine line, float timeSlot, int pixels)
    {
        bool talk = line.Kind == LobbyKind.Chat;
        Vector4 color = talk ? Talk : new Vector4(0.86f, 0.84f, 0.78f, 1f);
        string stamp = line.At.ToLocalTime().ToString("[HH:mm:ss]");
        float row = ImGui.GetCursorPosX();
        ImGui.TextColored(talk ? Talk : Ui.Muted, stamp);
        ImGui.SameLine();
        ImGui.SetCursorPosX(row + timeSlot);
        if (!talk)
        {
            Mark(line.Kind, pixels);
            ImGui.SameLine(0, 6);
        }

        string who = line.Role == StaffRole.None
            ? line.Name
            : $"{line.Name}  {StaffText.Label(line.Role)}";
        string body = $"{who}:  {line.Text}";
        if (talk)
        {
            ImGui.TextColored(color, body);
            return;
        }

        using (Ui.PushPixels(pixels, true))
            ImGui.TextColored(color, body);
    }

    private static unsafe void Play(int index)
    {
        int sound = Math.Clamp(index, 1, Sounds.Length);
        try
        {
            UIGlobals.PlaySoundEffect(Sounds[sound - 1].Id);
        }
        catch (Exception)
        {
        }
    }

    private static void Mark(LobbyKind kind, int pixels)
    {
        Vector4 color = kind switch
        {
            LobbyKind.Pass or LobbyKind.Register or LobbyKind.Join => Ui.Good,
            LobbyKind.Fail or LobbyKind.Remove or LobbyKind.Leave => Ui.Danger,
            LobbyKind.Threads => new Vector4(0.90f, 0.82f, 0.58f, 1f),
            LobbyKind.Run or LobbyKind.Role => Ui.Purple,
            _ => Ui.Yellow,
        };
        Vector2 origin = ImGui.GetCursorScreenPos();
        float size = Math.Max(12f, pixels);
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        Vector2 center = origin + new Vector2(size * 0.5f, size * 0.5f);
        uint ink = ImGui.GetColorU32(color);
        switch (kind)
        {
            case LobbyKind.Threads:
                Ui.ThreadMark(size);
                return;
            case LobbyKind.Run:
                Ui.RunMark(size);
                return;
            case LobbyKind.Pass:
                draw.AddCircleFilled(center, 6f, ink);
                break;
            case LobbyKind.Fail:
            case LobbyKind.Remove:
                draw.AddLine(center + new Vector2(-4, -4), center + new Vector2(4, 4), ink, 1.8f);
                draw.AddLine(center + new Vector2(4, -4), center + new Vector2(-4, 4), ink, 1.8f);
                break;
            case LobbyKind.Register:
                draw.AddLine(center + new Vector2(0, -5), center + new Vector2(0, 5), ink, 1.8f);
                draw.AddLine(center + new Vector2(-5, 0), center + new Vector2(5, 0), ink, 1.8f);
                break;
            default:
                draw.AddCircle(center, 5f, ink, 12, 1.4f);
                break;
        }

        ImGui.Dummy(new Vector2(size, size));
    }
}
