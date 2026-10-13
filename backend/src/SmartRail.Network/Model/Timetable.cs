namespace SmartRail.Network.Model;

public enum TrainType { Express, Intercity, Slow }

public enum CallType { Stop, Pass }

public class ServiceCalendar
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";              // "WEEKDAY"
    public DayOfWeek[] DaysOfWeek { get; set; } = [];
    public List<CalendarException> Exceptions { get; set; } = [];
}

// A date that differs from the weekly pattern, for example a Poya day.
public class CalendarException
{
    public Guid Id { get; set; }
    public Guid CalendarId { get; set; }
    public DateOnly Date { get; set; }
    public bool Runs { get; set; }
    public string Name { get; set; } = "";
}

public class Service
{
    public Guid Id { get; set; }
    public string Number { get; set; } = "";            // text ("1001"), unique; never used as an ID in URLs
    public Guid RouteId { get; set; }
    public TrainType TrainType { get; set; }
    public Guid CalendarId { get; set; }
    public List<ScheduledStop> Stops { get; set; } = [];
}

public class ScheduledStop
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public int Sequence { get; set; }
    public Guid StationId { get; set; }
    public CallType CallType { get; set; }

    // Minutes from the start of the service date, so an overnight journey stays on its service date.
    // The first stop has no arrival and the last has no departure; a Pass has the same value in both.
    public int? ArrivalOffset { get; set; }
    public int? DepartureOffset { get; set; }

    public string PlannedPlatform { get; set; } = "";
}
