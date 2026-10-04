namespace Pnx.Odyssey.Core;

public sealed class VenuePerson
{
    public string Name { get; set; } = "";

    public string World { get; set; } = "";

    public int Visits { get; set; }
}

public sealed class VenueTouch
{
    public string ParticipantId { get; set; } = "";

    public DateTime At { get; set; }

    public int Count { get; set; }
}
