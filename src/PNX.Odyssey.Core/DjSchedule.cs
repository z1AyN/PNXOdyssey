namespace Pnx.Odyssey.Core;

public static class DjSchedule
{
    public readonly record struct Pick(int Current, int Next);

    public static Pick Choose(IReadOnlyList<long> starts, long now)
    {
        int current = -1;
        int next = -1;
        for (int index = 0; index < starts.Count; index++)
        {
            if (starts[index] <= now)
            {
                current = index;
                continue;
            }

            next = index;
            break;
        }

        return new Pick(current, next);
    }

    public static string Shout(string? playing, string? link, string? upcoming)
    {
        if (string.IsNullOrWhiteSpace(playing) && string.IsNullOrWhiteSpace(upcoming))
            return "";
        if (string.IsNullOrWhiteSpace(playing))
            return $"Next on the decks: {upcoming} ({link}).";
        if (string.IsNullOrWhiteSpace(upcoming))
            return $"Now playing: {playing} ({link}).";
        return $"Now playing: {playing} ({link}). Next: {upcoming}.";
    }
}
