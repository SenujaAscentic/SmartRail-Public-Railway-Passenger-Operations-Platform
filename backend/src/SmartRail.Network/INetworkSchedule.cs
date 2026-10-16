namespace SmartRail.Network;

// The Network module's public interface for other modules (architecture Q3, Q5).
// Everything else in Network (tables, model classes, the seed loader) is internal.
// The records below are plain data copies: they carry no database types, so a reader cannot reach the tables.
public interface INetworkSchedule
{
    // The whole plan: what runs, on which days, and with which train sets. Small enough to read in one call.
    Task<NetworkSchedule> GetScheduleAsync(CancellationToken cancellationToken);
}

public sealed record NetworkSchedule(
    IReadOnlyList<ScheduledService> Services,
    IReadOnlyList<ServiceCalendarInfo> Calendars,
    IReadOnlyList<TrainSetInfo> TrainSets);

public sealed record ScheduledService(
    Guid Id,
    string Number,
    Guid CalendarId,
    IReadOnlyList<ScheduledStopInfo> Stops,
    Guid LineId);

public enum ScheduledCallType { Stop, Pass }

// Offsets are minutes after local midnight of the service date (domain model section 3).
public sealed record ScheduledStopInfo(
    int Sequence,
    Guid StationId,
    ScheduledCallType CallType,
    int? ArrivalOffset,
    int? DepartureOffset,
    string PlannedPlatform);

public sealed record ServiceCalendarInfo(
    Guid Id,
    IReadOnlyList<DayOfWeek> DaysOfWeek,
    IReadOnlyList<CalendarExceptionInfo> Exceptions);

// A date that differs from the weekly pattern: Runs = false means the service does not run that day (a Poya day).
public sealed record CalendarExceptionInfo(DateOnly Date, bool Runs);

public sealed record TrainSetInfo(Guid Id, string Code);
