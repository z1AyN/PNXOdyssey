using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Pnx.Odyssey.UI;

internal sealed class SymbolWindow : Window
{
    private int _set;

    public SymbolWindow() : base("Symbols##odyssey-symbols")
    {
        IsOpen = false;
        Size = new Vector2(520, 420);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public Action<string>? Apply { get; set; }

    public override void PreDraw() => Ui.PushFrame();

    public override void PostDraw() => Ui.PopFrame();

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("symbol-sets"))
            return;

        for (int index = 0; index < SymbolCatalog.Sets.Length; index++)
        {
            if (!ImGui.BeginTabItem(SymbolCatalog.Sets[index].Name))
                continue;
            _set = index;
            DrawSet(SymbolCatalog.Sets[index].Glyphs);
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawSet(string[] glyphs)
    {
        int column = 0;
        foreach (string glyph in glyphs)
        {
            if (ImGui.Button($"{glyph}##{glyph}", new Vector2(32, 28)))
                Apply?.Invoke(glyph);
            column++;
            if (column % 12 != 0)
                ImGui.SameLine();
        }
    }
}
