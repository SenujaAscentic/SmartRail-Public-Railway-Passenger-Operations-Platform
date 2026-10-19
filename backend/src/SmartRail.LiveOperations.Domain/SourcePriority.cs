namespace SmartRail.LiveOperations.Domain;

// Who supplied a value. The number is the priority: the lower number wins (domain model 4.1, R-05).
// The server sets this from the caller's credential, never from a request body.
public enum Source { Controller = 1, Crew = 2, Gps = 3, Schedule = 4 }

// A stop time and who recorded it.
public sealed record TimeWithSource(DateTime Value, Source Source)
{
    // R-05: the incoming value wins unless the current one comes from a higher-priority source.
    // The same source may overwrite itself, so a newer GPS fix replaces an older one.
    public static TimeWithSource Resolve(TimeWithSource? current, TimeWithSource incoming) =>
        current is not null && incoming.Source > current.Source ? current : incoming;
}

// The journey's current delay and who set it.
public sealed record SourcedDelay(Delay Value, Source Source)
{
    public static SourcedDelay FromSchedule { get; } = new(new Delay(0), Source.Schedule);

    // R-05 (clarified 15 Oct): the journey delay follows the most recent stop event whatever its source, so a crew tap
    // never freezes it. Only a controller delay blocks the others, until the controller changes or clears it.
    // (The source priority of TimeWithSource.Resolve protects a stop's own time, not this journey-wide value.)
    public SourcedDelay Apply(Delay delay, Source source) =>
        Source == Source.Controller && source != Source.Controller ? this : new SourcedDelay(delay, source);

    // Only a controller delay is cleared. It is replaced by the delay recalculated from the latest recorded stop time
    // (FromSchedule if there is none), never blindly by 0: a reset would show a false "On time" (US-P4) and could send
    // a wrong push (R-16). The caller works out `recalculated`; this type does not know the stops.
    public SourcedDelay ClearController(SourcedDelay recalculated) =>
        Source == Source.Controller ? recalculated : this;
}
