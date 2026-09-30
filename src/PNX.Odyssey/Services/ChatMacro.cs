using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using InteropGenerator.Runtime;

namespace Pnx.Odyssey.Services;

/// <summary>Runs a temporary macro through the game shell without touching saved macros.</summary>
internal static unsafe class ChatMacro
{
    private const int MaxLines = 15;
    private const int MaxLineBytes = 180;

    public static bool Run(IReadOnlyList<string> commands)
    {
        if (commands.Count == 0 || commands.Count > MaxLines)
            return false;

        try
        {
            var macro = default(RaptureMacroModule.Macro);
            macro.Name.Ctor();
            Span<Utf8String> lines = macro.Lines;
            for (int index = 0; index < lines.Length; index++)
                lines[index].Ctor();

            for (int index = 0; index < commands.Count; index++)
            {
                string line = commands[index];
                if (line.Length == 0 || line.IndexOfAny(['\r', '\n', '\0']) >= 0)
                    return false;
                if (Encoding.UTF8.GetByteCount(line) > MaxLineBytes)
                    return false;

                byte[] bytes = SeString.Parse(Encoding.UTF8.GetBytes(line)).Encode();
                if (bytes.Length == 0 || bytes.Any(value => value == 0))
                    return false;

                fixed (byte* pointer = bytes)
                    lines[index].SetString((CStringPointer)pointer);
            }

            RaptureShellModule* shell = RaptureShellModule.Instance();
            if (shell == null)
                return false;

            shell->ExecuteMacro(&macro);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
