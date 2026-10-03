using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Pnx.Odyssey.Core;

namespace Pnx.Odyssey.UI;

internal sealed class LibraryWindow : Window
{
    private const string FilterMine = "__mine__";
    private const string FilterAll = "__all__";

    private readonly Plugin _plugin;
    private bool _editing;
    private bool _viewOnly;
    private string _editId = "";
    private string _editName = "";
    private string _editText = "/p ";
    private string _editSymbol = "";
    private string _filter = FilterAll;

    public LibraryWindow(Plugin plugin) : base("Shared Library##odyssey-lib")
    {
        _plugin = plugin;
        IsOpen = false;
        Size = new Vector2(820, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(640, 360),
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
        const float sideWidth = 180f;
        float height = Math.Max(120f, ImGui.GetContentRegionAvail().Y);

        if (ImGui.BeginChild("lib-side", new Vector2(sideWidth, height), true))
            DrawSidebar(snapshot, self);
        ImGui.EndChild();

        ImGui.SameLine();
        if (ImGui.BeginChild("lib-main", new Vector2(0, height)))
            DrawMacroList(snapshot, self);
        ImGui.EndChild();
    }

    private void DrawSidebar(SessionSnapshot snapshot, string self)
    {
        if (ImGui.Selectable("Your Macros", _filter == FilterMine))
            _filter = FilterMine;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Selectable("All", _filter == FilterAll))
            _filter = FilterAll;

        foreach (Creator creator in Creators(snapshot, self))
        {
            ImGui.PushID(creator.Id);
            if (ImGui.Selectable(creator.Name, _filter == creator.Id))
                _filter = creator.Id;
            ImGui.PopID();
        }
    }

    private void DrawMacroList(SessionSnapshot snapshot, string self)
    {
        if (ImGui.Button("New macro"))
        {
            _editing = true;
            _viewOnly = false;
            _editId = "";
            _editName = "New macro";
            _editText = "/p ";
            _editSymbol = "";
            _filter = FilterMine;
        }

        ImGui.SameLine();
        Ui.Hint(FilterLabel(snapshot, self));

        IEnumerable<GodMacro> macros = snapshot.Macros.Where(macro => Matches(macro, self));
        bool any = false;
        foreach (GodMacro macro in macros)
        {
            any = true;
            ImGui.PushID(macro.Id);
            ImGui.AlignTextToFramePadding();
            ImGui.Text(macro.Name);
            if (_filter == FilterAll || _filter == FilterMine)
            {
                ImGui.SameLine();
                Ui.Hint(macro.AuthorName);
            }

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

            if (hot)
            {
                ImGui.SameLine();
                _plugin.Config.MacroButtonColors.TryGetValue(macro.Id, out string? hex);
                Vector4 fill = Ui.ParseColor(hex, Ui.DefaultMacroColor);
                if (ImGui.ColorEdit4(
                        $"##lib-color-{macro.Id}",
                        ref fill,
                        ImGuiColorEditFlags.NoAlpha
                        | ImGuiColorEditFlags.NoInputs
                        | ImGuiColorEditFlags.NoLabel))
                {
                    _plugin.Config.MacroButtonColors[macro.Id] = Ui.FormatColor(fill);
                    _plugin.Config.Save();
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Hotbar button colour");
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

        if (!any)
            Ui.Hint("No macros here yet.");
    }

    private bool Matches(GodMacro macro, string self)
    {
        if (_filter == FilterAll)
            return true;
        if (_filter == FilterMine)
            return string.Equals(macro.AuthorId, self, StringComparison.OrdinalIgnoreCase);
        return string.Equals(macro.AuthorId, _filter, StringComparison.OrdinalIgnoreCase);
    }

    private string FilterLabel(SessionSnapshot snapshot, string self)
    {
        if (_filter == FilterAll)
            return "All macros";
        if (_filter == FilterMine)
            return "Your macros";
        Creator? creator = Creators(snapshot, self).FirstOrDefault(item => item.Id == _filter);
        return creator?.Name ?? "Macros";
    }

    private static List<Creator> Creators(SessionSnapshot snapshot, string self)
    {
        return snapshot.Macros
            .Where(macro => !string.IsNullOrWhiteSpace(macro.AuthorId)
                && !string.Equals(macro.AuthorId, self, StringComparison.OrdinalIgnoreCase))
            .GroupBy(macro => macro.AuthorId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new Creator(
                group.Key,
                group.Select(macro => macro.AuthorName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                ?? group.Key))
            .OrderBy(creator => creator.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void DrawEditor()
    {
        if (_viewOnly)
            ImGui.Text(_editName);
        else
        {
            ImGui.SetNextItemWidth(280);
            ImGui.InputText("Name", ref _editName, 40);
        }

        if (!_viewOnly)
        {
            string symbolLabel = string.IsNullOrEmpty(_editSymbol) ? "Symbols" : $"Symbols {_editSymbol}";
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Icons, symbolLabel))
            {
                _plugin.OpenSymbols(symbol =>
                {
                    _editSymbol = symbol;
                    _editText = Place(_editText, symbol);
                });
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Insert a decorative symbol after each chat command.");
        }

        string text = _editText;
        ImGuiInputTextFlags flags = _viewOnly ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None;
        float close = ImGui.GetFrameHeightWithSpacing() * (_viewOnly ? 1f : 2f);
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
        _editSymbol = "";
    }

    private static string Place(string text, string symbol)
    {
        string[] lines = text
            .Replace("\r", "", StringComparison.Ordinal)
            .Split('\n');
        for (int index = 0; index < lines.Length; index++)
            lines[index] = PlaceLine(lines[index], symbol);
        return string.Join('\n', lines);
    }

    private static string PlaceLine(string line, string symbol)
    {
        string trimmed = line.TrimEnd();
        if (trimmed.Length == 0)
            return line;
        if (trimmed.StartsWith("/wait", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        int space = trimmed.IndexOf(' ');
        if (trimmed.StartsWith('/') && space > 0)
        {
            string command = trimmed[..(space + 1)];
            string body = trimmed[(space + 1)..].TrimStart();
            if (body.StartsWith(symbol, StringComparison.Ordinal))
                return trimmed;
            return $"{command}{symbol} {body}";
        }

        if (trimmed.StartsWith(symbol, StringComparison.Ordinal))
            return trimmed;
        return $"{symbol} {trimmed}";
    }

    private readonly record struct Creator(string Id, string Name);
}
