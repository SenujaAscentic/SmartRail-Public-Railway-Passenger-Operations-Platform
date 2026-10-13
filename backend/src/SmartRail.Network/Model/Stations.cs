namespace SmartRail.Network.Model;

// Real, verified data (names, order, distances, coordinates): see shared/seed/README.md.
public class Station
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";              // "KDT"; unique, used to find the row again when the seed is reloaded
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int ZoneRadiusM { get; set; }                // detection zone around the station (R-04)
    public List<Platform> Platforms { get; set; } = [];
}

public class Platform
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public string Number { get; set; } = "";            // text, because some stations use "1A"
    public bool InService { get; set; }
}
