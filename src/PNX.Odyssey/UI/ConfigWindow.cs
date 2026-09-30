using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Pnx.Odyssey.UI;

internal sealed class ConfigWindow : Window
{
    private readonly Plugin _plugin;
    private string _key = "";
    private string _macro = "";

    public ConfigWindow(Plugin plugin) : base("PNX Odyssey Settings##config")
    {
        _plugin = plugin;
        Size = new Vector2(560, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Load()
    {
        _key = _plugin.Config.PluginKey;
        _macro = _plugin.Config.GameStartMacro;
    }

    public override void PreDraw() => Ui.PushFrame();

    public override void PostDraw() => Ui.PopFrame();

    public override void Draw()
    {
        Ui.Section("Connection");
        ImGui.TextWrapped("Everyone with the plugin joins the same live table.");
        Ui.Hint(Configuration.ServerAddress);
        Ui.Space();
        Label("Plugin key");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##plugin-key", ref _key, 128, ImGuiInputTextFlags.Password);
        Ui.Section("World progress");
        Ui.Hint("Follows each character. Clamp uses Height Pos for everyone. Offset is added either way.");
        bool clamp = _plugin.Config.OverlayClamp;
        float height = _plugin.Config.OverlayHeight;
        float offset = _plugin.Config.OverlayOffset;
        float scale = _plugin.Config.OverlayScale;
        float opacity = _plugin.Config.OverlayOpacity;
        bool edited = false;
        if (ImGui.Checkbox("Clamp to height", ref clamp))
            _plugin.Config.OverlayClamp = clamp;
        edited |= ImGui.IsItemDeactivatedAfterEdit();
        Label("Adjust Height Pos");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderFloat("##height", ref height, 0.8f, 4.5f, "%.2f"))
            _plugin.Config.OverlayHeight = height;
        edited |= ImGui.IsItemDeactivatedAfterEdit();
        Label("Height offset");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderFloat("##offset", ref offset, -1.5f, 1.5f, "%.2f"))
            _plugin.Config.OverlayOffset = offset;
        edited |= ImGui.IsItemDeactivatedAfterEdit();
        Label("Adjust size");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderFloat("##size", ref scale, 0.6f, 2.2f, "%.2f"))
            _plugin.Config.OverlayScale = scale;
        edited |= ImGui.IsItemDeactivatedAfterEdit();
        Label("Adjust transparency");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderFloat("##opacity", ref opacity, 0.15f, 1f, "%.2f"))
            _plugin.Config.OverlayOpacity = opacity;
        edited |= ImGui.IsItemDeactivatedAfterEdit();
        if (edited)
            _plugin.Config.Save();
        Ui.Section("Game start");
        ImGui.InputTextMultiline("##macro", ref _macro, 800, new Vector2(-1, 120));
        Ui.Space();
        if (ImGui.Button("Save", new Vector2(96, 0)))
            {
                _plugin.Config.ServerUrl = Configuration.ServerAddress;
                _plugin.Config.PluginKey = _key.Trim();
                _plugin.Config.GameStartMacro = _macro;
                _plugin.Config.Save();
                _plugin.Client.Configure(Configuration.ServerAddress, Configuration.PluginKeyValue);
            }
    }

    private static void Label(string text) => ImGui.TextColored(Ui.Muted, text);
}
