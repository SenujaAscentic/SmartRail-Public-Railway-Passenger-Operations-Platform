using SmartRail.Network.Seed;

namespace SmartRail.UnitTests.Network.Seed;

// A tiny valid network the tests start from and then break on purpose:
// stations AAA - BBB - CCC (BBB has 2 platforms), one route, one service that stops at all three.
internal static class SeedTestData
{
    public static NetworkSeedData Valid()
    {
        var stations = new List<StationSeed>
        {
            new(1, "AAA", "Alpha", 0.000m, 6.9, 79.8, 3, "Official list"),
            new(2, "BBB", "Beta", 10.000m, 7.0, 79.9, 2, "Official list"),
            new(3, "CCC", "Gamma", 25.000m, 7.1, 80.0, 1, "Official list"),
        };

        var route = new RouteSeed("R-UP", "L1", "Up", "Alpha to Gamma",
        [
            new(1, "AAA", 0.000m),
            new(2, "BBB", 10.000m),
            new(3, "CCC", 25.000m),
        ]);

        var service = new ServiceSeed("1001", "R-UP", "Express", "WEEKDAY",
        [
            new(1, "AAA", "Stop", null, 360, "1"),
            new(2, "BBB", "Stop", 380, 382, "2"),
            new(3, "CCC", "Stop", 400, null, "1"),
        ]);

        var calendar = new CalendarSeed("WEEKDAY", ["Mon", "Tue", "Wed", "Thu", "Fri"], []);
        var trainSet = new TrainSetSeed("TS-1", "DieselPowerSet",
        [
            new(1, "First", "Reserved"),
            new(2, "Second", "Unreserved"),
        ]);

        return new NetworkSeedData(stations, [new LineSeed("L1", "Line one")], 300, [route], [service], [calendar], [trainSet]);
    }

    // Replaces the stops of the only service.
    public static NetworkSeedData WithStops(this NetworkSeedData data, params ScheduledStopSeed[] stops) =>
        data with { Services = [data.Services[0] with { Stops = stops }] };

    // Replaces the station at the given position (0-based) in the station list.
    public static NetworkSeedData WithStation(this NetworkSeedData data, int index, StationSeed station)
    {
        var stations = data.Stations.ToList();
        stations[index] = station;
        return data with { Stations = stations };
    }
}
