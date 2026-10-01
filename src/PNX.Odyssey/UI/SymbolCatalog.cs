namespace Pnx.Odyssey.UI;

internal static class SymbolCatalog
{
    public static readonly (string Name, string[] Glyphs)[] Sets =
    [
        ("Common", Build([
            (0xE031, 0xE03F), (0xE040, 0xE044), (0xE048, 0xE04E), (0xE050, 0xE05E),
            (0xE06A, 0xE06F), (0xE070, 0xE070), (0xE0AF, 0xE0AF), (0xE0BA, 0xE0BF), (0xE0C0, 0xE0C0),
        ], "★☆♠♡♢♣♤♥♦♧♪♭♯")),
        ("Symbols", Build([
            (0xE020, 0xE02B), (0xE031, 0xE035), (0xE037, 0xE03F), (0xE040, 0xE044),
            (0xE048, 0xE04E), (0xE050, 0xE05F), (0xE060, 0xE06F), (0xE070, 0xE07F),
            (0xE080, 0xE08A), (0xE08F, 0xE08F), (0xE090, 0xE09F), (0xE0A0, 0xE0AF),
            (0xE0B0, 0xE0BF), (0xE0C0, 0xE0C6), (0xE0D0, 0xE0DB), (0xE0E0, 0xE0E9),
        ], "★☆♠♡♢♣♤♥♦♧♪♭♯°。・○◎●□■△▼◆◇☀☁☂☃℃℉←↑→↓⇔⇒©®™℡№§¶$€¥£¢¤円∀∂∃⊇⊂≠≡≦∽∫∥∙∋+-=")),
        ("Numbers", Build([
            (0xE060, 0xE069), (0xE08F, 0xE09F), (0xE0A0, 0xE0AE), (0xE0B1, 0xE0B9), (0xE0E0, 0xE0E9),
        ], "⓪①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳⑴⑵⑶⑷⑸⑹⑺⑻⑼⑽⑾⑿⒀⒁⒂⒃⒄⒅⒆⒇⒈⒉⒊⒋⒌⒍⒎⒏⒐")),
        ("Letters", Build([(0xE022, 0xE022), (0xE024, 0xE024), (0xE071, 0xE07F), (0xE080, 0xE08A)], "")),
        ("Time", Build([], "\uE031\uE06B\uE06D\uE06E\uE0D0\uE0D1\uE0D2")),
        ("Others", Build([
            (0xE0D9, 0xE0DB), (0xE0C1, 0xE0C6), (0xE020, 0xE021), (0xE023, 0xE023),
            (0xE025, 0xE02B), (0xE050, 0xE05A),
        ], "☀☁☂☃℃℉°。・○◎●□■△▼◆◇←↑→↓⇔⇒©®™℡№§¶$€¥£¢¤円∀∂∃⊇⊂≠≡≦∽∫∥∙∋+-=")),
    ];

    private static string[] Build((int Start, int End)[] ranges, string extra)
    {
        var symbols = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(string symbol)
        {
            if (symbol.Length > 0 && seen.Add(symbol))
                symbols.Add(symbol);
        }

        foreach ((int start, int end) in ranges)
        {
            for (int code = start; code <= end; code++)
                Add(char.ConvertFromUtf32(code));
        }

        for (int index = 0; index < extra.Length; index++)
        {
            if (char.IsWhiteSpace(extra, index))
                continue;
            int code = char.ConvertToUtf32(extra, index);
            if (char.IsHighSurrogate(extra[index]))
                index++;
            Add(char.ConvertFromUtf32(code));
        }

        return symbols.ToArray();
    }
}
