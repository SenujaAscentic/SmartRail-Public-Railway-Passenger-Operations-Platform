namespace SmartRail.LiveOperations.Domain;

// Everything the journey needs to judge one incoming position. The slice builds it: it knows the clock offset of the
// crew session, and it measures the distances (route and stations live in the Network module, not here).
//   DistanceFromRouteMetres   distance to the nearest point of the route (R-08)
//   DistanceToStationsMetres  distance to each station the slice wants checked, by station id; stations left out are
//                             not checked (normally the next stops ahead). The journey never invents a distance.
public sealed record PositionSample(
    GeoPoint Location,
    double SpeedKmh,
    DateTime? GpsTime,
    DateTime DeviceTime,
    TimeSpan ClockOffset,
    double DistanceFromRouteMetres,
    IReadOnlyDictionary<Guid, double> DistanceToStationsMetres);
