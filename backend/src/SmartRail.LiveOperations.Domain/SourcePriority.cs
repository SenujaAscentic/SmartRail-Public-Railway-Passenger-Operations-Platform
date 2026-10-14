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

    // R-05: same rule as TimeWithSource.Resolve. A controller delay therefore stays until the controller changes or clears it.
    public SourcedDelay Apply(Delay delay, Source source) =>
        source > Source ? this : new SourcedDelay(delay, source);

    // Only a controller delay is cleared. Back to "on schedule" until the next GPS or crew value arrives, which then applies.
    public SourcedDelay ClearController() =>
        Source == Source.Controller ? FromSchedule : this;
}
