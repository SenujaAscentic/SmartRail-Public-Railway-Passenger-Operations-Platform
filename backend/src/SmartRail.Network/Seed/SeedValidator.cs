using SmartRail.Network.Model;

namespace SmartRail.Network.Seed;

// One value per invariant, so a test (and a log line) can say exactly which rule broke.
public enum SeedRule
{
    DuplicateStationCode,
    DuplicateKey,
    UnknownValue,
    UnknownLine,
    SequenceNotContiguous,
    DistanceSourceWrong,
    DistanceSourceEstimate,
    UnknownStation,
    RouteDistanceNotIncreasing,
    UnknownRoute,
    UnknownCalendar,
    StopOrder,
    StopStationNotOnRoute,
    StopTimesMissing,
    PassTimesDiffer,
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

        var lineIds = CheckLines(data.Lines, errors);
        var stations = CheckStations(data.Stations, errors, warnings);
        var routes = CheckRoutes(data.Routes, stations, lineIds, errors);
        var calendarIds = CheckCalendars(data.Calendars, errors);
        CheckTrainSets(data.TrainSets, errors);

        var serviceNumbers = new HashSet<string>();
        foreach (var service in data.Services)
        {
            if (!serviceNumbers.Add(service.Number))
            {
                errors.Add(new(SeedRule.DuplicateKey, $"Service number {service.Number} appears more than once."));
            }

            CheckService(service, stations, routes, calendarIds, errors);
        }

        return new SeedValidationResult(errors, warnings);
    }

    // The text values in the files are turned into enums by the loader, so they must be known names.
    private static bool IsKnown<T>(string value) where T : struct, Enum => Enum.IsDefined(typeof(T), value);

    // True for 1, 2, 3 ... n with no gaps and no repeats (in any order).
    private static bool IsOneToN(IEnumerable<int> values) =>
        values.Order().SequenceEqual(Enumerable.Range(1, values.Count()));

    private static HashSet<string> CheckLines(IReadOnlyList<LineSeed> rows, List<SeedIssue> errors)
    {
        var ids = new HashSet<string>();

        foreach (var line in rows)
        {
            if (!ids.Add(line.Id))
            {
                errors.Add(new(SeedRule.DuplicateKey, $"Line {line.Id} appears more than once."));
            }
        }

        return ids;
    }

    // Returns the calendar ids.
    private static HashSet<string> CheckCalendars(IReadOnlyList<CalendarSeed> rows, List<SeedIssue> errors)
    {
        var ids = new HashSet<string>();

        foreach (var calendar in rows)
        {
            if (!ids.Add(calendar.Id))
            {
                errors.Add(new(SeedRule.DuplicateKey, $"Calendar {calendar.Id} appears more than once."));
            }

            foreach (var day in calendar.DaysOfWeek.Where(d => !SeedDays.ByName.ContainsKey(d)))
            {
                errors.Add(new(SeedRule.UnknownValue, $"Calendar {calendar.Id}: \"{day}\" is not a day (use Mon, Tue, Wed, Thu, Fri, Sat, Sun)."));
            }

            var dates = new HashSet<DateOnly>();
            foreach (var exception in calendar.Exceptions.Where(e => !dates.Add(e.Date)))
            {
                errors.Add(new(SeedRule.DuplicateKey, $"Calendar {calendar.Id}: the exception date {exception.Date:yyyy-MM-dd} appears more than once."));
            }
        }

        return ids;
    }

    private static void CheckTrainSets(IReadOnlyList<TrainSetSeed> rows, List<SeedIssue> errors)
    {
        var codes = new HashSet<string>();

        foreach (var trainSet in rows)
        {
            if (!codes.Add(trainSet.Code))
            {
                errors.Add(new(SeedRule.DuplicateKey, $"Train set {trainSet.Code} appears more than once."));
            }

            if (!IsKnown<Traction>(trainSet.Traction))
            {
                errors.Add(new(SeedRule.UnknownValue, $"Train set {trainSet.Code}: unknown traction \"{trainSet.Traction}\"."));
            }

            if (!IsOneToN(trainSet.Coaches.Select(c => c.Position)))
            {
                errors.Add(new(SeedRule.SequenceNotContiguous, $"Train set {trainSet.Code}: coach positions must be 1, 2, 3 ... with no gaps or repeats."));
            }

            foreach (var coach in trainSet.Coaches)
            {
                if (!IsKnown<CoachClass>(coach.Class))
                {
                    errors.Add(new(SeedRule.UnknownValue, $"Train set {trainSet.Code}, coach {coach.Position}: unknown class \"{coach.Class}\"."));
                }

                if (!IsKnown<SeatingType>(coach.Seating))
                {
                    errors.Add(new(SeedRule.UnknownValue, $"Train set {trainSet.Code}, coach {coach.Position}: unknown seating \"{coach.Seating}\"."));
                }
            }
        }
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
        IReadOnlyList<RouteSeed> rows, Dictionary<string, StationSeed> stations, HashSet<string> lineIds, List<SeedIssue> errors)
    {
        var byId = new Dictionary<string, RouteSeed>();

        foreach (var route in rows)
        {
            if (!byId.TryAdd(route.Id, route))
            {
                errors.Add(new(SeedRule.DuplicateKey, $"Route {route.Id} appears more than once."));
                continue;
            }

            if (!lineIds.Contains(route.LineId))
            {
                errors.Add(new(SeedRule.UnknownLine, $"Route {route.Id}: line {route.LineId} does not exist."));
            }

            if (!IsKnown<Direction>(route.Direction))
            {
                errors.Add(new(SeedRule.UnknownValue, $"Route {route.Id}: unknown direction \"{route.Direction}\" (use Up or Down)."));
            }

            if (!IsOneToN(route.Stations.Select(s => s.Sequence)))
            {
                errors.Add(new(SeedRule.SequenceNotContiguous, $"Route {route.Id}: station sequence must be 1, 2, 3 ... with no gaps or repeats."));
            }

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
        if (!IsKnown<TrainType>(service.TrainType))
        {
            errors.Add(new(SeedRule.UnknownValue, $"Service {service.Number}: unknown train type \"{service.TrainType}\"."));
        }

        if (!IsOneToN(service.Stops.Select(s => s.Sequence)))
        {
            errors.Add(new(SeedRule.SequenceNotContiguous, $"Service {service.Number}: stop sequence must be 1, 2, 3 ... with no gaps or repeats."));
        }

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

            if (!IsKnown<CallType>(stop.CallType))
            {
                errors.Add(new(SeedRule.UnknownValue, $"{where}: unknown call type \"{stop.CallType}\" (use Stop or Pass)."));
            }

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

            // Convention (13 Oct): a Pass has one passing time, written as both arrival and departure.
            if (stop.CallType == "Pass" && stop.ArrivalOffset is int passArrival && stop.DepartureOffset is int passDeparture
                && passArrival != passDeparture)
            {
                errors.Add(new(SeedRule.PassTimesDiffer, $"{where}: a Pass must have the same arrival and departure time."));
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
