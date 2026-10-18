using SmartRail.LiveOperations.Domain;
using SmartRail.Network;

namespace SmartRail.LiveOperations.Features.CrewPositions;

// Where along the route a train is, as the stream shows it (TrainPositionUpdated): between which two stations, and how far.
public readonly record struct RouteProgress(Guid LastStationId, Guid NextStationId, double FractionToNext);

// The route of a service: its stations joined by straight lines (the seed has no track shape). Pure, so it is unit-tested.
// The slice measures with it and hands the numbers to the Domain (R-04 and R-08 take distances as input, domain model 5).
//
// Limit, logged 18 Oct: a real track bends between stations, so on a long section a real GPS fix can be more than the 500 m of
// R-08 away from the straight line and be discarded. The simulator follows the same lines, so the demo is unaffected; real tablets
// would need waypoints in the seed.
internal sealed class RouteGeometry(IReadOnlyList<RoutePoint> points)
{
    private const double EarthRadiusMetres = 6_371_000;

    public bool IsEmpty => points.Count < 2;

    // R-08: the distance from the nearest point of the route.
    public double DistanceFromRouteMetres(GeoPoint position) => Nearest(position).DistanceMetres;

    // R-04: the distance to each station (from the position to the station's coordinates).
    public IReadOnlyDictionary<Guid, double> DistancesToStations(GeoPoint position, IEnumerable<Guid> stationIds)
    {
        var byId = points.ToDictionary(p => p.StationId);
        return stationIds.Where(byId.ContainsKey).Distinct().ToDictionary(
            id => id, id => position.DistanceMetresTo(new GeoPoint(byId[id].Latitude, byId[id].Longitude)));
    }

    // Between which two stations the nearest point of the route is, and how far from the first to the second (0 to 1).
    public RouteProgress ProgressOf(GeoPoint position)
    {
        var nearest = Nearest(position);
        return new RouteProgress(points[nearest.Segment].StationId, points[nearest.Segment + 1].StationId, nearest.Fraction);
    }

    private (int Segment, double Fraction, double DistanceMetres) Nearest(GeoPoint position)
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException("The route has fewer than two stations.");
        }

        var best = (Segment: 0, Fraction: 0.0, DistanceMetres: double.MaxValue);
        for (var i = 0; i < points.Count - 1; i++)
        {
            var candidate = ToSegment(position, points[i], points[i + 1]);
            if (candidate.DistanceMetres < best.DistanceMetres)
            {
                best = (i, candidate.Fraction, candidate.DistanceMetres);
            }
        }

        return best;
    }

    // The distance from the position to the segment A-B, on a flat map centred on the position (metres east and north). Over the
    // tens of kilometres of one section the flattening error is far smaller than the 500 m the result is compared with.
    private static (double Fraction, double DistanceMetres) ToSegment(GeoPoint position, RoutePoint a, RoutePoint b)
    {
        var (ax, ay) = Flat(position, a);
        var (bx, by) = Flat(position, b);
        var (dx, dy) = (bx - ax, by - ay);
        var length2 = dx * dx + dy * dy;

        // The position is the origin (0, 0); the closest point of A-B is A + t * (B - A), with t kept between 0 and 1.
        var t = length2 == 0 ? 0 : Math.Clamp(-(ax * dx + ay * dy) / length2, 0, 1);
        var (cx, cy) = (ax + t * dx, ay + t * dy);
        return (t, Math.Sqrt(cx * cx + cy * cy));
    }

    private static (double East, double North) Flat(GeoPoint origin, RoutePoint point)
    {
        var metresPerDegree = Math.PI / 180 * EarthRadiusMetres;
        return ((point.Longitude - origin.Longitude) * Math.Cos(origin.Latitude * Math.PI / 180) * metresPerDegree,
                (point.Latitude - origin.Latitude) * metresPerDegree);
    }
}
