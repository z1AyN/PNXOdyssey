using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace Pnx.Odyssey.Services;

internal static unsafe class ServerClock
{
    public static long Unix()
    {
        try
        {
            Framework* framework = Framework.Instance();
            if (framework == null)
                return DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            long unix = Framework.GetServerTime();
            if (unix < 1_000_000_000)
                return DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return unix;
        }
        catch (Exception)
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    public static string Clock()
    {
        return DateTimeOffset.FromUnixTimeSeconds(Unix()).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
