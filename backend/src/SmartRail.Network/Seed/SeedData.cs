namespace SmartRail.Network.Seed;

// The shapes of the files in shared/seed/, exactly as written there.
// Text fields (direction, call type, ...) stay strings here so the validator can report a bad value
// with a clear message; the loader turns them into enums only after validation passes.

public sealed record StationSeed(
    int Sequence, string Code, string Name, decimal DistanceKm,
    double Latitude, double Longitude, int Platforms, string DistanceSource);

public sealed record LineSeed(string Id, string Name);

public sealed record RouteStationSeed(int Sequence, string StationCode, decimal DistanceKm);

public sealed record RouteSeed(
    string Id, string LineId, string Direction, string Name, IReadOnlyList<RouteStationSeed> Stations);

public sealed record ScheduledStopSeed(
    int Sequence, string StationCode, string CallType,
    int? ArrivalOffset, int? DepartureOffset, string PlannedPlatform);

public sealed record ServiceSeed(
    string Number, string RouteId, string TrainType, string CalendarId, IReadOnlyList<ScheduledStopSeed> Stops);

public sealed record CalendarExceptionSeed(DateOnly Date, bool Runs, string Name);

public sealed record CalendarSeed(
    string Id, IReadOnlyList<string> DaysOfWeek, IReadOnlyList<CalendarExceptionSeed> Exceptions);

public sealed record CoachSeed(int Position, string Class, string Seating);

public sealed record TrainSetSeed(string Code, string Traction, IReadOnlyList<CoachSeed> Coaches);

// Everything the Network module loads, in one object.
public sealed record NetworkSeedData(
    IReadOnlyList<StationSeed> Stations,
    IReadOnlyList<LineSeed> Lines,
    int ZoneRadiusM,
    IReadOnlyList<RouteSeed> Routes,
    IReadOnlyList<ServiceSeed> Services,
    IReadOnlyList<CalendarSeed> Calendars,
    IReadOnlyList<TrainSetSeed> TrainSets);
