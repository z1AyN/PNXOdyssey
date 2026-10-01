namespace Pnx.Odyssey.Core;

public sealed class AspectClaim
{
    public string Id { get; set; } = "";

    public string OfferingId { get; set; } = "";

    public string ParticipantId { get; set; } = "";

    public string PlayerName { get; set; } = "";

    public int Run { get; set; } = 1;

    public DateTime At { get; set; }

    public AspectClaim Clone() => new()
    {
        Id = Id,
        OfferingId = OfferingId,
        ParticipantId = ParticipantId,
        PlayerName = PlayerName,
        Run = Run,
        At = At,
    };
}

public static class AspectCatalog
{
    public sealed record Offering(string Id, string Name, string Text, string PoolId, int? Slots, bool Shared);

    public static IReadOnlyList<Offering> All { get; } = Build();

    private static readonly Dictionary<string, Offering> ById =
        All.ToDictionary(offering => offering.Id, StringComparer.Ordinal);

    public static Offering? Find(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : ById.GetValueOrDefault(id);

    public static int Used(IEnumerable<AspectClaim> claims, Offering offering) =>
        claims.Count(claim => Find(claim.OfferingId) is Offering other && other.PoolId == offering.PoolId);

    public static bool HasRoom(Offering offering, IEnumerable<AspectClaim> claims) =>
        offering.Slots is not int slots || Used(claims, offering) < slots;

    public static string ClaimsText(Offering offering, IEnumerable<AspectClaim> claims)
    {
        if (offering.Slots is not int slots)
            return "Unlimited";

        int left = Math.Max(0, slots - Used(claims, offering));
        if (!offering.Shared)
            return left.ToString();

        return left == 1
            ? "Shared pool: 1 rainbow claim"
            : $"Shared pool: {left} rainbow claims";
    }

    public static string ClaimLog(AspectClaim claim)
    {
        Offering? offering = Find(claim.OfferingId);
        string aspect = offering?.Name ?? "an aspect";
        return offering == null
            ? $"{claim.PlayerName} claimed the Aspect of {aspect}"
            : $"{claim.PlayerName} claimed the Aspect of {aspect} · {offering.Text}";
    }

    public static string? Discord(string? aspectName) =>
        string.IsNullOrWhiteSpace(aspectName) ? null : Discords.GetValueOrDefault(aspectName);

    private static readonly Dictionary<string, string> Discords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Missy Umbra"] = "missyumbra",
        ["Ink Sen’en"] = "shiningink",
        ["Yua Tsukihana"] = "yua.tsukihana",
        ["Ramune Soda"] = "sodapon.",
        ["Ellie Gator"] = "elliegator9",
        ["Vanitas Enetari"] = "yugudoll",
        ["Steffomatus Torus"] = "steffomatus",
        ["Chocola Strawberry"] = "choco62089",
        ["Niyu Moon"] = "youkuo",
        ["Kori Ketsueki"] = "demivex",
        ["Leo Noie"] = "leonoie",
        ["Xeron Akshara"] = ".xeron.",
        ["Sugar Mist"] = "sugarmistgaming",
        ["Loony Lee"] = "loonylein",
        ["Cassian Hyskaris"] = "kia.the.bun",
        ["Wok Around"] = "woklbokl",
        ["Black Rabbit"] = "neuneles",
        ["Rin Ferron"] = "jlah",
        ["Bean Buns"] = "adrianwrobel",
        ["Rosen Hue"] = "rosenhue",
        ["Ash Yusira"] = "ash_mori",
        ["Cota Arulaq"] = "_cota",
    };

    private static Offering[] Build() =>
    [
        Solo("ash-voice", "Ash Yusira", "Voice recording, such as reading or ASMR", 2),
        Solo("bean-gpose", "Bean Buns", "Themed, edited GPose", 1),
        Solo("rabbit-gpose", "Black Rabbit", "GPose", 2),
        Pool("cassian-rp", "Cassian Hyskaris", "4-hour SFW or NSFW RP experience", "cassian", 1),
        Pool("cassian-makeup", "Cassian Hyskaris", "Make-up work", "cassian", 1),
        Solo("chocola-hang", "Chocola Strawberry", "Hanging out", 1),
        Solo("chocola-gpose", "Chocola Strawberry", "SFW duo GPose with Chocola", 1),
        Solo("cota-coupon", "Cota Arulaq", "Commission coupon", 3),
        Solo("ellie-art", "Ellie Gator", "Character art commission — chibi or kawaii", 3),
        Solo("ink-portrait", "Ink Sen’en", "Portrait art commission", 2),
        Open("ink-raid", "Ink Sen’en", "Raiding partner"),
        Open("ink-blackjack", "Ink Sen’en", "Private blackjack"),
        Pool("kori-upscale", "Kori Ketsueki", "Upscales and ports to TBSE bodies, Muse or Neolithe", "kori", 2),
        Pool("kori-gpose", "Kori Ketsueki", "GPose", "kori", 2),
        Solo("leo-gpose", "Leo Noie", "GPose with doodles", 3),
        Solo("loony-art", "Loony Lee", "Thigh-up, fully rendered artwork of your OC", 1),
        Solo("missy-gpose", "Missy Umbra", "GPose commission", 3),
        Solo("missy-tarot", "Missy Umbra", "Tarot reading", 2),
        Solo("niyu-gpose", "Niyu Moon", "GPose", 2),
        Pool("ramune-hang", "Ramune Soda", "Hanging out in-game or on voice", "ramune", 2),
        Pool("ramune-content", "Ramune Soda", "Non-endgame content partner", "ramune", 2),
        Pool("ramune-gaming", "Ramune Soda", "Gaming", "ramune", 2),
        Pool("ramune-movie", "Ramune Soda", "Movie night", "ramune", 2),
        Solo("rin-gpose", "Rin Ferron", "GPose", 1),
        Pool("rosen-rp", "Rosen Hue", "RP scene, such as matcha or tea-leaf reading", "rosen", 2),
        Pool("rosen-date", "Rosen Hue", "Companionship for an RP date at another event or venue", "rosen", 2),
        Solo("steff-blackjack", "Steffomatus Torus", "Blackjack hosting — 2 hours", 1),
        Solo("steff-paint", "Steffomatus Torus", "MS Paint drawing of a Pokémon of your choice", 1),
        Solo("sugar-gpose", "Sugar Mist", "Solo or duo GPose", 1),
        Solo("vanitas-art", "Vanitas Enetari", "Large Greek mythology illustration — approximately 20–30 hours of work", 1),
        Solo("wok-gpose", "Wok Around", "GPose commission", 2),
        Solo("xeron-chibi", "Xeron Akshara", "Chibi art commission of one character", 1),
        Solo("yua-gpose", "Yua Tsukihana", "GPose", 1),
        Solo("yua-rp", "Yua Tsukihana", "Immersive RP — 4 hours", 1),
        Solo("yua-portrait", "Yua Tsukihana", "Portrait/bust-up art commission", 1),
    ];

    private static Offering Solo(string id, string name, string text, int slots) =>
        new(id, name, text, id, slots, false);

    private static Offering Open(string id, string name, string text) =>
        new(id, name, text, id, null, false);

    private static Offering Pool(string id, string name, string text, string pool, int slots) =>
        new(id, name, text, pool, slots, true);
}
