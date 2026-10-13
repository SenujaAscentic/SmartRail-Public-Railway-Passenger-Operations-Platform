namespace SmartRail.Network.Seed;

// One value per invariant, so a test (and a log line) can say exactly which rule broke.
public enum SeedRule
{
    DuplicateStationCode,
    DistanceSourceWrong,
    DistanceSourceEstimate,
    UnknownStation,
    RouteDistanceNotIncreasing,
    UnknownRoute,
    UnknownCalendar,
    StopOrder,
    StopStationNotOnRoute,
    StopTimesMissing,
    DepartureBeforeArrival,
    TimeGoesBackwards,
    FirstStopHasArrival,
    LastStopHasDeparture,
    FirstOrLastStopIsPass,
    PlannedPlatformUnknown,
}

public sealed record SeedIssue(SeedRule Rule, string Message);

public sealed record SeedValidationResult(IReadOnlyList<SeedIssue> Errors, IReadOnlyList<SeedIssue> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

// Checks the invariants of domain model section 3 (plus basic sanity of the seed files).
// Pure code: no database, no files, no clock. It collects every problem instead of stopping at the first.
public static class SeedValidator
{
    public static SeedValidationResult Validate(NetworkSeedData data)
    {
        var errors = new List<SeedIssue>();
        var warnings = new List<SeedIssue>();

        var stations = CheckStations(data.Stations, errors, warnings);
        var routes = CheckRoutes(data.Routes, stations, errors);
        var calendarIds = data.Calendars.Select(c => c.Id).ToHashSet();

        foreach (var service in data.Services)
        {
            CheckService(service, stations, routes, calendarIds, errors);
        }

        return new SeedValidationResult(errors, warnings);
    }

    // Returns the stations by code (first one wins when a code is repeated).
    private static Dictionary<string, StationSeed> CheckStations(
        IReadOnlyList<StationSeed> rows, List<SeedIssue> errors, List<SeedIssue> warnings)
    {
        var byCode = new Dictionary<string, StationSeed>();

        foreach (var station in rows)
        {
            if (!byCode.TryAdd(station.Code, station))
            {
                errors.Add(new(SeedRule.DuplicateStationCode, $"Station code {station.Code} appears more than once."));
                continue;
            }

            if (station.DistanceSource.Contains("WRONG", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new(SeedRule.DistanceSourceWrong,
                    $"Station {station.Code} ({station.Name}): the distance is marked WRONG in its distance_source. Fix stations.csv first."));
            }
            else if (station.DistanceSource.Contains("ESTIMATE", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(new(SeedRule.DistanceSourceEstimate,
                    $"Station {station.Code} ({station.Name}): the distance is an ESTIMATE. Replace it with the official figure."));
            }
        }

        return byCode;
    }

    // Returns the routes by id.
    private static Dictionary<string, RouteSeed> CheckRoutes(
        IReadOnlyList<RouteSeed> rows, Dictionary<string, StationSeed> stations, List<SeedIssue> errors)
    {
        var byId = rows.ToDictionary(r => r.Id);

        foreach (var route in rows)
        {
            decimal? previousDistance = null;

            foreach (var routeStation in route.Stations.OrderBy(s => s.Sequence))
            {
                if (!stations.ContainsKey(routeStation.StationCode))
                {
                    errors.Add(new(SeedRule.UnknownStation,
                        $"Route {route.Id}: station {routeStation.StationCode} is not in the station list."));
                }

                if (previousDistance is not null && routeStation.DistanceKm <= previousDistance)
                {
                    errors.Add(new(SeedRule.RouteDistanceNotIncreasing,
                        $"Route {route.Id}: distance at {routeStation.StationCode} ({routeStation.DistanceKm} km) is not greater than the one before it ({previousDistance} km)."));
                }

                previousDistance = routeStation.DistanceKm;
            }
        }

        return byId;
    }

    private static void CheckService(
        ServiceSeed service,
        Dictionary<string, StationSeed> stations,
        Dictionary<string, RouteSeed> routes,
        HashSet<string> calendarIds,
        List<SeedIssue> errors)
    {
        if (!calendarIds.Contains(service.CalendarId))
        {
            errors.Add(new(SeedRule.UnknownCalendar, $"Service {service.Number}: calendar {service.CalendarId} does not exist."));
        }

        if (!routes.TryGetValue(service.RouteId, out var route))
        {
            errors.Add(new(SeedRule.UnknownRoute, $"Service {service.Number}: route {service.RouteId} does not exist."));
            return;   // without a route there is nothing to compare the stops with
        }

        // Position of each station along the route: 0 for the first, 1 for the next, ...
        var routeIndex = route.Stations
            .OrderBy(s => s.Sequence)
            .Select((s, index) => (s.StationCode, index))
            .ToDictionary(x => x.StationCode, x => x.index);

        var stops = service.Stops;
        var previousRouteIndex = -1;
        int? previousTime = null;   // the last time seen on this service: previous departure, or arrival if it has none

        for (var i = 0; i < stops.Count; i++)
        {
            var stop = stops[i];
            var where = $"Service {service.Number}, stop {stop.Sequence} ({stop.StationCode})";

            // Stops must be on the route and in the route's direction of travel.
            if (!routeIndex.TryGetValue(stop.StationCode, out var index))
            {
                errors.Add(new(SeedRule.StopStationNotOnRoute, $"{where}: the station is not on route {route.Id}."));
            }
            else
            {
                if (index <= previousRouteIndex)
                {
                    errors.Add(new(SeedRule.StopOrder, $"{where}: stops are not in the order of route {route.Id}."));
                }

                previousRouteIndex = index;
            }

            // The first stop has no arrival; the last has no departure; neither can be a Pass.
            var isFirst = i == 0;
            var isLast = i == stops.Count - 1;

            if (isFirst && stop.ArrivalOffset is not null)
            {
                errors.Add(new(SeedRule.FirstStopHasArrival, $"{where}: the first stop must not have an arrival time."));
            }

            if (isLast && stop.DepartureOffset is not null)
            {
                errors.Add(new(SeedRule.LastStopHasDeparture, $"{where}: the last stop must not have a departure time."));
            }

            if ((isFirst || isLast) && stop.CallType == "Pass")
            {
                errors.Add(new(SeedRule.FirstOrLastStopIsPass, $"{where}: the first and last stop must be real stops, not a Pass."));
            }

            // Every middle stop has both times, also a Pass: the train enters the station zone and leaves it (R-04).
            // They may be the same whole minute, but neither may be missing.
            if (!isFirst && !isLast && (stop.ArrivalOffset is null || stop.DepartureOffset is null))
            {
                errors.Add(new(SeedRule.StopTimesMissing, $"{where}: a middle stop needs both an arrival and a departure time."));
            }

            // Times: departure is not before arrival, and time never runs backwards along the service.
            if (stop.ArrivalOffset is int arrival && stop.DepartureOffset is int departure && departure < arrival)
            {
                errors.Add(new(SeedRule.DepartureBeforeArrival, $"{where}: departure ({departure}) is before arrival ({arrival})."));
            }

            var firstTimeHere = stop.ArrivalOffset ?? stop.DepartureOffset;
            if (previousTime is int before && firstTimeHere is int now && now < before)
            {
                errors.Add(new(SeedRule.TimeGoesBackwards, $"{where}: time {now} is earlier than the previous time {before}."));
            }

            previousTime = stop.DepartureOffset ?? stop.ArrivalOffset ?? previousTime;

            // The planned platform must exist at the station: platforms are numbered 1 to N.
            if (stations.TryGetValue(stop.StationCode, out var station)
                && !(int.TryParse(stop.PlannedPlatform, out var platform) && platform >= 1 && platform <= station.Platforms))
            {
                errors.Add(new(SeedRule.PlannedPlatformUnknown,
                    $"{where}: platform \"{stop.PlannedPlatform}\" does not exist (the station has {station.Platforms})."));
            }
        }
    }
}
