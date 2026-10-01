using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.UI;

internal sealed class LibraryWindow : Window
{
    private readonly Plugin _plugin;
    private bool _editing;
    private bool _viewOnly;
    private string _editId = "";
    private string _editName = "";
    private string _editText = "/p ";

    public LibraryWindow(Plugin plugin) : base("Shared Library##odyssey-lib")
    {
        _plugin = plugin;
        IsOpen = false;
        Size = new Vector2(760, 460);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 320),
            MaximumSize = new Vector2(1200, 900),
        };
    }

    public override void PreDraw() => Ui.PushFrame();

    public override void PostDraw() => Ui.PopFrame();

    public override void Draw()
    {
        SessionSnapshot? snapshot = _plugin.Client.Snapshot;
        if (snapshot == null)
        {
            Ui.Hint("Join a session to use the shared library.");
            return;
        }

        if (_editing)
            DrawEditor();
        else
            DrawList(snapshot);
    }

    private void DrawList(SessionSnapshot snapshot)
    {
        string self = _plugin.Client.SelfId ?? "";
        if (ImGui.Button("New macro"))
        {
            _editing = true;
            _viewOnly = false;
            _editId = "";
            _editName = "New macro";
            _editText = "/p ";
        }

        foreach (GodMacro macro in snapshot.Macros)
        {
            ImGui.PushID(macro.Id);
            ImGui.AlignTextToFramePadding();
            ImGui.Text(macro.Name);
            ImGui.SameLine();
            Ui.Hint(macro.AuthorName);
            ImGui.SameLine();
            bool hot = _plugin.Config.GodHotbar.Contains(macro.Id);
            if (ImGui.SmallButton(hot ? "On hotbar" : "Add to hotbar"))
            {
                if (hot)
                    _plugin.Config.GodHotbar.Remove(macro.Id);
                else
                    _plugin.Config.GodHotbar.Add(macro.Id);
                _plugin.Config.Save();
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("View"))
                Open(macro, viewOnly: true);

            if (string.Equals(macro.AuthorId, self, StringComparison.OrdinalIgnoreCase))
            {
                ImGui.SameLine();
                if (ImGui.SmallButton("Edit"))
                    Open(macro, viewOnly: false);
                ImGui.SameLine();
                if (ImGui.SmallButton("Delete"))
                    _plugin.Client.RemoveGodMacro(macro.Id);
            }

            ImGui.PopID();
        }
    }

    private void DrawEditor()
    {
        if (_viewOnly)
            ImGui.Text(_editName);
        else
        {
            ImGui.SetNextItemWidth(280);
            ImGui.InputText("Name", ref _editName, 40);
            ImGui.SameLine();
            if (ImGui.Button("Symbols"))
                _plugin.OpenSymbols(symbol => _editText = Place(_editText, symbol));
        }

        string text = _editText;
        ImGuiInputTextFlags flags = _viewOnly ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None;
        float close = ImGui.GetFrameHeightWithSpacing();
        Vector2 body = new(ImGui.GetContentRegionAvail().X, Math.Max(80f, ImGui.GetContentRegionAvail().Y - close));
        if (ImGui.InputTextMultiline("##god-body", ref text, 900, body, flags) && !_viewOnly)
            _editText = text;

        if (_viewOnly)
        {
            if (ImGui.Button("Back"))
                _editing = false;
            return;
        }

        if (ImGui.Button("Save"))
        {
            _plugin.Client.SaveGodMacro(string.IsNullOrEmpty(_editId) ? null : _editId, _editName.Trim(), _editText.Trim());
            _editing = false;
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
            _editing = false;
    }

    private void Open(GodMacro macro, bool viewOnly)
    {
        _editing = true;
        _viewOnly = viewOnly;
        _editId = macro.Id;
        _editName = macro.Name;
        _editText = macro.Text;
    }

    private static string Place(string text, string symbol)
    {
        int space = text.IndexOf(' ');
        if (text.StartsWith('/') && space > 0)
            return text[..(space + 1)] + symbol + " " + text[(space + 1)..];
        return $"{symbol} {text}";
    }
}
