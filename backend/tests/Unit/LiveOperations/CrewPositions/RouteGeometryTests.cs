using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.CrewPositions;
using SmartRail.Network;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations.CrewPositions;

// The route is the stations joined by straight lines; the numbers it gives feed R-04 and R-08.
public class RouteGeometryTests
{
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();

    // A is west, B is 0.1 degrees (about 11 km) east of A, C is 0.1 degrees (about 11 km) north of B: an L-shaped route near 7 N.
    private static readonly RouteGeometry Route = new(
        [new RoutePoint(A, 7.0, 80.0), new RoutePoint(B, 7.0, 80.1), new RoutePoint(C, 7.1, 80.1)]);

    [Fact]
    public void A_point_on_the_route_is_at_distance_zero()
    {
        Assert.Equal(0, Route.DistanceFromRouteMetres(new GeoPoint(7.0, 80.05)), precision: 0);
        Assert.Equal(0, Route.DistanceFromRouteMetres(new GeoPoint(7.05, 80.1)), precision: 0);
    }

    [Fact]
    public void A_point_off_the_route_is_measured_to_the_nearest_segment()
    {
        // 0.005 degrees of latitude north of the first segment is about 556 m.
        var distance = Route.DistanceFromRouteMetres(new GeoPoint(7.005, 80.05));

        Assert.InRange(distance, 540, 570);
    }

    [Fact]
    public void A_point_beyond_the_end_is_measured_to_the_last_station()
    {
        // 0.01 degrees north of C is about 1112 m.
        Assert.InRange(Route.DistanceFromRouteMetres(new GeoPoint(7.11, 80.1)), 1090, 1135);
    }

    [Fact]
    public void Progress_is_the_two_stations_around_the_nearest_point_and_the_fraction_between_them()
    {
        var halfway = Route.ProgressOf(new GeoPoint(7.0, 80.05));
        Assert.Equal((A, B), (halfway.LastStationId, halfway.NextStationId));
        Assert.Equal(0.5, halfway.FractionToNext, precision: 2);

        var onSecond = Route.ProgressOf(new GeoPoint(7.075, 80.1));
        Assert.Equal((B, C), (onSecond.LastStationId, onSecond.NextStationId));
        Assert.Equal(0.75, onSecond.FractionToNext, precision: 2);
    }

    [Fact]
    public void Progress_is_kept_between_zero_and_one_before_the_start_and_after_the_end()
    {
        var before = Route.ProgressOf(new GeoPoint(7.0, 79.9));
        Assert.Equal((A, B, 0.0), (before.LastStationId, before.NextStationId, before.FractionToNext));

        var after = Route.ProgressOf(new GeoPoint(7.2, 80.1));
        Assert.Equal((B, C, 1.0), (after.LastStationId, after.NextStationId, after.FractionToNext));
    }

    [Fact]
    public void Distances_to_stations_are_straight_line_distances_to_their_coordinates_and_unknown_stations_are_left_out()
    {
        var distances = Route.DistancesToStations(new GeoPoint(7.0, 80.0), [A, B, Guid.NewGuid()]);

        Assert.Equal(2, distances.Count);
        Assert.Equal(0, distances[A], precision: 0);
        Assert.InRange(distances[B], 11_000, 11_250);   // 0.1 degrees of longitude at 7 N
    }

    [Fact]
    public void A_route_with_fewer_than_two_stations_is_empty_and_cannot_be_measured()
    {
        var empty = new RouteGeometry([new RoutePoint(A, 7.0, 80.0)]);

        Assert.True(empty.IsEmpty);
        Assert.Throws<InvalidOperationException>(() => empty.DistanceFromRouteMetres(new GeoPoint(7, 80)));
    }
}
