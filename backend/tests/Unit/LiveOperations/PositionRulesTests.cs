using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// R-08 (position plausibility) and R-09 (clock correction).
public class PositionRulesTests
{
    private static readonly DateTime T0 = new(2026, 10, 14, 4, 0, 0, DateTimeKind.Utc);

    // 0.01 degrees of latitude is about 1112 m anywhere on Earth.
    private static readonly GeoPoint Start = new(7.0, 80.0);
    private static GeoPoint NorthBy(double degrees) => new(7.0 + degrees, 80.0);

    private static readonly AcceptedPosition Last = new(Start, T0);

    [Fact]
    public void Geo_distance_of_0_01_degrees_latitude_is_about_1112_metres()
    {
        Assert.Equal(1112, Start.DistanceMetresTo(NorthBy(0.01)), precision: 0);
    }

    [Fact]
    public void First_position_is_accepted()
    {
        Assert.Equal(PositionVerdict.Accepted, PositionPlausibility.Check(null, Start, T0, distanceFromRouteMetres: 10));
    }

    [Fact]
    public void Plausible_position_is_accepted()
    {
        // 890 m in 30 s is about 107 km/h.
        var verdict = PositionPlausibility.Check(Last, NorthBy(0.008), T0.AddSeconds(30), distanceFromRouteMetres: 20);

        Assert.Equal(PositionVerdict.Accepted, verdict);
    }

    [Fact]
    public void Position_older_than_last_accepted_is_discarded()
    {
        var verdict = PositionPlausibility.Check(Last, NorthBy(0.001), T0.AddSeconds(-5), distanceFromRouteMetres: 0);

        Assert.Equal(PositionVerdict.OlderThanLastAccepted, verdict);
    }

    // Equal time is not newer either. It would also divide by zero when working out the speed.
    [Fact]
    public void Position_with_the_same_time_as_the_last_accepted_is_discarded()
    {
        var verdict = PositionPlausibility.Check(Last, NorthBy(0.001), T0, distanceFromRouteMetres: 0);

        Assert.Equal(PositionVerdict.OlderThanLastAccepted, verdict);
    }

    [Fact]
    public void Position_more_than_500m_from_route_is_discarded()
    {
        var verdict = PositionPlausibility.Check(Last, NorthBy(0.001), T0.AddSeconds(10), distanceFromRouteMetres: 501);

        Assert.Equal(PositionVerdict.OffRoute, verdict);
    }

    [Fact]
    public void Position_exactly_500m_from_route_is_accepted()
    {
        var verdict = PositionPlausibility.Check(Last, NorthBy(0.001), T0.AddSeconds(10), distanceFromRouteMetres: 500);

        Assert.Equal(PositionVerdict.Accepted, verdict);
    }

    [Fact]
    public void Position_implying_more_than_120_kmh_is_discarded()
    {
        // 1112 m in 30 s is about 133 km/h.
        var verdict = PositionPlausibility.Check(Last, NorthBy(0.01), T0.AddSeconds(30), distanceFromRouteMetres: 0);

        Assert.Equal(PositionVerdict.ImpliedSpeedTooHigh, verdict);
    }

    [Fact]
    public void Event_time_uses_gps_time_over_device_clock()
    {
        var gps = T0;
        var device = T0.AddMinutes(7);   // the tablet's clock is wrong

        Assert.Equal(gps, ClockCorrection.EventTime(gps, device, TimeSpan.FromMinutes(-7)));
    }

    [Fact]
    public void Event_time_without_gps_is_device_time_plus_the_clock_offset()
    {
        // The offset was measured at sign-in as server time minus device time: the tablet runs 7 min fast.
        var device = T0.AddMinutes(7);

        Assert.Equal(T0, ClockCorrection.EventTime(null, device, TimeSpan.FromMinutes(-7)));
    }
}
