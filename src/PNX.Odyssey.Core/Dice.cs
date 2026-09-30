using System.Globalization;
using System.Text.RegularExpressions;

namespace Pnx.Odyssey.Core;

public readonly record struct RollSet(IReadOnlyList<int> Faces)
{
    public bool IsComplete => Faces.Count == TrialRules.DicePerSet;

    public int Total => Faces.Sum();

    public int OddCount => Faces.Count(face => (face & 1) == 1);
}

public static class DiceGrouping
{
    public static IReadOnlyList<RollSet> Group(IReadOnlyList<int> rolls)
    {
        if (rolls.Count == 0)
            return [];

        var sets = new List<RollSet>((rolls.Count + 2) / 3);
        for (int index = 0; index < rolls.Count; index += TrialRules.DicePerSet)
        {
            int count = Math.Min(TrialRules.DicePerSet, rolls.Count - index);
            var faces = new int[count];
            for (int offset = 0; offset < count; offset++)
                faces[offset] = rolls[index + offset];
            sets.Add(new RollSet(faces));
        }

        return sets;
    }

    public static RollSet Latest(IReadOnlyList<int> rolls)
    {
        IReadOnlyList<RollSet> sets = Group(rolls);
        return sets.Count == 0 ? new RollSet([]) : sets[^1];
    }
}

public static partial class DiceText
{
    public static int? ReadRoll(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        Match resultBeforeRange = ResultBeforeRange().Match(text);
        if (resultBeforeRange.Success
            && int.TryParse(resultBeforeRange.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int beforeRange))
            return beforeRange;

        MatchCollection numbers = RollPattern().Matches(text);
        if (numbers.Count == 0)
            return null;

        // Party dice lines include a leading 1 in front of the real result.
        Match chosen = numbers.Count > 1 && numbers[0].Value == "1"
            ? numbers[^1]
            : numbers[0];
        return int.TryParse(chosen.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            ? value
            : null;
    }

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex RollPattern();

    [GeneratedRegex(@"(\d+)\s*\(out of", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ResultBeforeRange();
}
