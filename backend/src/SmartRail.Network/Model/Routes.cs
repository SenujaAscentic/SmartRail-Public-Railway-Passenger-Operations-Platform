namespace SmartRail.Network.Model;

internal enum Direction { Up, Down }   // Up = Fort to Kandy, Down = Kandy to Fort

// One line in the MVP (Main Line). Kept as an entity so a second line is new data, not new code.
internal class Line
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";              // "MAIN"
    public string Name { get; set; } = "";
    public List<Route> Routes { get; set; } = [];
}

// One route per direction.
internal class Route
{
    public Guid Id { get; set; }
    public Guid LineId { get; set; }
    public string Code { get; set; } = "";              // "MAIN-UP"
    public Direction Direction { get; set; }
    public string Name { get; set; } = "";
    public List<RouteStation> Stations { get; set; } = [];
}

// The ordered list of stations on a route, with the distance from the route's start.
internal class RouteStation
{
    public Guid Id { get; set; }
    public Guid RouteId { get; set; }
    public int Sequence { get; set; }
    public Guid StationId { get; set; }
    public decimal DistanceKm { get; set; }
}
