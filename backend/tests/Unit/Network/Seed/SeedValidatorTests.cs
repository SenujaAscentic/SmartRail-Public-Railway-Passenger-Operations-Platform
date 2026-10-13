using SmartRail.Network.Seed;
using Xunit;

namespace SmartRail.UnitTests.Network.Seed;

public class SeedValidatorTests
{
    private static void AssertError(NetworkSeedData data, SeedRule expected)
    {
        var result = SeedValidator.Validate(data);
        Assert.Contains(result.Errors, e => e.Rule == expected);
    }

    [Fact]
    public void A_valid_network_has_no_errors_or_warnings()
    {
        var result = SeedValidator.Validate(SeedTestData.Valid());

        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    // ---- stations ----

    [Fact]
    public void Duplicate_station_codes_are_rejected()
    {
        var data = SeedTestData.Valid();
        data = data.WithStation(2, data.Stations[2] with { Code = "AAA" });

        AssertError(data, SeedRule.DuplicateStationCode);
    }

    [Fact]
    public void Station_row_with_WRONG_in_distance_source_is_refused()
    {
        var data = SeedTestData.Valid();
        data = data.WithStation(1, data.Stations[1] with { DistanceSource = "DERIVED - WRONG: too short" });

        AssertError(data, SeedRule.DistanceSourceWrong);
    }

    [Fact]
    public void Station_row_with_ESTIMATE_is_accepted_with_one_warning_naming_the_station()
    {
        var data = SeedTestData.Valid();
        data = data.WithStation(2, data.Stations[2] with { DistanceSource = "ESTIMATE - straight line x 1.2" });

        var result = SeedValidator.Validate(data);

        Assert.Empty(result.Errors);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(SeedRule.DistanceSourceEstimate, warning.Rule);
        Assert.Contains("CCC", warning.Message);
        Assert.Contains("Gamma", warning.Message);
    }

    // ---- routes ----

    [Fact]
    public void Route_distances_not_increasing_in_sequence_order_are_rejected()
    {
        var data = SeedTestData.Valid();
        var route = data.Routes[0] with
        {
            Stations = [data.Routes[0].Stations[0], data.Routes[0].Stations[1] with { DistanceKm = 30.000m }, data.Routes[0].Stations[2]],
        };
        data = data with { Routes = [route] };   // 0, 30, 25: goes backwards

        AssertError(data, SeedRule.RouteDistanceNotIncreasing);
    }

    [Fact]
    public void Route_referencing_an_unknown_station_is_rejected()
    {
        var data = SeedTestData.Valid();
        var route = data.Routes[0] with
        {
            Stations = [data.Routes[0].Stations[0], data.Routes[0].Stations[1] with { StationCode = "ZZZ" }, data.Routes[0].Stations[2]],
        };
        data = data with { Routes = [route] };

        AssertError(data, SeedRule.UnknownStation);
    }

    // ---- services ----

    [Fact]
    public void Service_referencing_an_unknown_route_is_rejected()
    {
        var data = SeedTestData.Valid();
        data = data with { Services = [data.Services[0] with { RouteId = "NO-SUCH-ROUTE" }] };

        AssertError(data, SeedRule.UnknownRoute);
    }

    [Fact]
    public void Service_referencing_an_unknown_calendar_is_rejected()
    {
        var data = SeedTestData.Valid();
        data = data with { Services = [data.Services[0] with { CalendarId = "NO-SUCH-CALENDAR" }] };

        AssertError(data, SeedRule.UnknownCalendar);
    }

    [Fact]
    public void Stops_not_in_route_order_are_rejected()
    {
        // Gamma is visited before Beta, but the route goes Alpha, Beta, Gamma.
        var data = SeedTestData.Valid().WithStops(
            new(1, "AAA", "Stop", null, 360, "1"),
            new(2, "CCC", "Stop", 380, 382, "1"),
            new(3, "BBB", "Stop", 400, null, "2"));

        AssertError(data, SeedRule.StopOrder);
    }

    [Fact]
    public void Stop_at_station_not_on_route_is_rejected()
    {
        var data = SeedTestData.Valid() with
        {
            Stations = [.. SeedTestData.Valid().Stations, new(4, "DDD", "Delta", 30.000m, 7.2, 80.1, 1, "Official list")],
        };
        data = data.WithStops(
            new(1, "AAA", "Stop", null, 360, "1"),
            new(2, "DDD", "Stop", 400, null, "1"));   // DDD exists but is not on route R-UP

        AssertError(data, SeedRule.StopStationNotOnRoute);
    }

    [Fact]
    public void Departure_before_arrival_is_rejected()
    {
        var data = SeedTestData.Valid().WithStops(
            new(1, "AAA", "Stop", null, 360, "1"),
            new(2, "BBB", "Stop", 382, 380, "2"),     // leaves 2 minutes before it arrives
            new(3, "CCC", "Stop", 400, null, "1"));

        AssertError(data, SeedRule.DepartureBeforeArrival);
    }

    [Fact]
    public void Arrival_before_the_previous_departure_is_rejected()
    {
        var data = SeedTestData.Valid().WithStops(
            new(1, "AAA", "Stop", null, 360, "1"),
            new(2, "BBB", "Stop", 355, 357, "2"),     // arrives at 355, but left Alpha at 360
            new(3, "CCC", "Stop", 400, null, "1"));

        AssertError(data, SeedRule.TimeGoesBackwards);
    }

    // A middle stop needs both times, also a Pass: the train enters the station zone and leaves it (R-04),
    // and those two moments can fall in the same minute but are never missing.
    [Theory]
    [InlineData("Stop")]
    [InlineData("Pass")]
    public void Middle_stop_without_any_time_is_rejected(string callType)
    {
        var data = SeedTestData.Valid().WithStops(
            new(1, "AAA", "Stop", null, 360, "1"),
            new(2, "BBB", callType, null, null, "2"),
            new(3, "CCC", "Stop", 400, null, "1"));

        AssertError(data, SeedRule.StopTimesMissing);
    }

    [Theory]
    [InlineData("Stop")]
    [InlineData("Pass")]
    public void Middle_stop_with_only_one_time_is_rejected(string callType)
    {
        var data = SeedTestData.Valid().WithStops(
            new(1, "AAA", "Stop", null, 360, "1"),
            new(2, "BBB", callType, 380, null, "2"),   // arrival but no departure
            new(3, "CCC", "Stop", 400, null, "1"));

        AssertError(data, SeedRule.StopTimesMissing);
    }

    [Fact]
    public void Pass_stop_with_the_same_arrival_and_departure_minute_is_accepted()
    {
        var data = SeedTestData.Valid().WithStops(
            new(1, "AAA", "Stop", null, 360, "1"),
            new(2, "BBB", "Pass", 380, 380, "2"),
            new(3, "CCC", "Stop", 400, null, "1"));

        Assert.Empty(SeedValidator.Validate(data).Errors);
    }

    [Fact]
    public void First_stop_with_an_arrival_is_rejected()
    {
        var data = SeedTestData.Valid().WithStops(
            new(1, "AAA", "Stop", 355, 360, "1"),
            new(2, "BBB", "Stop", 380, 382, "2"),
            new(3, "CCC", "Stop", 400, null, "1"));

        AssertError(data, SeedRule.FirstStopHasArrival);
    }

    [Fact]
    public void Last_stop_with_a_departure_is_rejected()
    {
        var data = SeedTestData.Valid().WithStops(
            new(1, "AAA", "Stop", null, 360, "1"),
            new(2, "BBB", "Stop", 380, 382, "2"),
            new(3, "CCC", "Stop", 400, 405, "1"));

        AssertError(data, SeedRule.LastStopHasDeparture);
    }

    [Fact]
    public void First_stop_being_a_Pass_is_rejected()
    {
        var data = SeedTestData.Valid().WithStops(
            new(1, "AAA", "Pass", null, 360, "1"),
            new(2, "BBB", "Stop", 380, 382, "2"),
            new(3, "CCC", "Stop", 400, null, "1"));

        AssertError(data, SeedRule.FirstOrLastStopIsPass);
    }

    [Fact]
    public void Last_stop_being_a_Pass_is_rejected()
    {
        var data = SeedTestData.Valid().WithStops(
            new(1, "AAA", "Stop", null, 360, "1"),
            new(2, "BBB", "Stop", 380, 382, "2"),
            new(3, "CCC", "Pass", 400, null, "1"));

        AssertError(data, SeedRule.FirstOrLastStopIsPass);
    }

    [Fact]
    public void Planned_platform_not_at_station_is_rejected()
    {
        // BBB has 2 platforms, so platform 3 does not exist there.
        var data = SeedTestData.Valid().WithStops(
            new(1, "AAA", "Stop", null, 360, "1"),
            new(2, "BBB", "Stop", 380, 382, "3"),
            new(3, "CCC", "Stop", 400, null, "1"));

        AssertError(data, SeedRule.PlannedPlatformUnknown);
    }
}
