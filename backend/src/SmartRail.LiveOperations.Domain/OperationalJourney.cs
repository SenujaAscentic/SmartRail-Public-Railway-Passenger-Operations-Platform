namespace SmartRail.LiveOperations.Domain;

// Only what daily generation (US-B2) needs. Delay, hold, tracking and the other statuses come with their own rules.
public enum JourneyStatus { Planned }

public enum CallType { Stop, Pass }

// One stop of a service as the timetable gives it: times are minutes after local midnight of the service date.
public sealed record PlannedStop(
    int Sequence, Guid StationId, CallType CallType,
    int? ArrivalOffset, int? DepartureOffset, string PlannedPlatform);

public sealed class JourneyStop
{
    public int Sequence { get; private set; }
    public Guid StationId { get; private set; }
    public CallType CallType { get; private set; }
    public DateTime? ScheduledArrival { get; private set; }     // UTC
    public DateTime? ScheduledDeparture { get; private set; }   // UTC
    public string PlannedPlatform { get; private set; } = "";

    private JourneyStop() { }   // for EF Core, which fills the properties itself

    internal JourneyStop(int sequence, Guid stationId, CallType callType, DateTime? arrival, DateTime? departure, string plannedPlatform)
    {
        Sequence = sequence;
        StationId = stationId;
        CallType = callType;
        ScheduledArrival = arrival;
        ScheduledDeparture = departure;
        PlannedPlatform = plannedPlatform;
    }
}

// One run of a service on one date (domain model section 4.1).
public sealed class OperationalJourney
{
    private readonly List<JourneyStop> _stops = [];

    public Guid Id { get; private set; }
    public Guid ServiceId { get; private set; }
    public DateOnly ServiceDate { get; private set; }
    public Guid TrainSetId { get; private set; }
    public JourneyStatus Status { get; private set; }
    public IReadOnlyList<JourneyStop> Stops => _stops;

    private OperationalJourney() { }   // for EF Core

    // Creates the journey for a service on a service date. The timetable's offsets are minutes after midnight on that
    // date in the given time zone (Asia/Colombo); they are converted to UTC here, once, and never again.
    public static OperationalJourney Plan(
        Guid id, Guid serviceId, DateOnly serviceDate, Guid trainSetId,
        IEnumerable<PlannedStop> stops, TimeZoneInfo timeZone)
    {
        var journey = new OperationalJourney
        {
            Id = id,
            ServiceId = serviceId,
            ServiceDate = serviceDate,
            TrainSetId = trainSetId,
            Status = JourneyStatus.Planned,
        };

        foreach (var stop in stops.OrderBy(s => s.Sequence))
        {
            journey._stops.Add(new JourneyStop(
                stop.Sequence, stop.StationId, stop.CallType,
                ToUtc(serviceDate, stop.ArrivalOffset, timeZone),
                ToUtc(serviceDate, stop.DepartureOffset, timeZone),
                stop.PlannedPlatform));
        }

        return journey;
    }

    // Local midnight of the service date plus the offset, as a wall-clock time in the zone, converted to UTC.
    // An offset above 1440 (an overnight run) simply lands on the next calendar day, still in this journey.
    private static DateTime? ToUtc(DateOnly serviceDate, int? offsetMinutes, TimeZoneInfo timeZone)
    {
        if (offsetMinutes is null)
        {
            return null;
        }

        var local = serviceDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified).AddMinutes(offsetMinutes.Value);
        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }
}
