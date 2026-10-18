namespace SmartRail.Api.Clock;

// A clock for rehearsing and demonstrating: it starts at a chosen moment and then runs at a chosen speed. The timetable is fixed
// (the first train leaves at 06:00), so a demo given at 20:00 needs the application to believe it is 05:55; "Speed" lets a rehearsal
// run faster than real time (speed 10: ten minutes pass in one). DEVELOPMENT ONLY, see ClockSetup.
//
// It wraps the real clock and changes only what time it reports; everything that asks `TimeProvider` (token lifetimes, delays,
// freshness, timers) therefore sees the same shifted time. Elapsed-time measurement (GetTimestamp) stays real.
internal sealed class DemoClock(DateTimeOffset start, double speed, TimeProvider real) : TimeProvider
{
    private readonly DateTimeOffset _realStart = real.GetUtcNow();

    public double Speed { get; } = speed;

    // GetUtcNow must answer with offset zero (the database refuses any other offset), whatever offset the start was written with.
    public override DateTimeOffset GetUtcNow() => (start + (real.GetUtcNow() - _realStart) * Speed).ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone => real.LocalTimeZone;

    public override long TimestampFrequency => real.TimestampFrequency;

    public override long GetTimestamp() => real.GetTimestamp();

    // A timer asked to fire after 30 clock-seconds fires after 30 / speed real seconds, so timers keep step with GetUtcNow().
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        real.CreateTimer(callback, state, Scale(dueTime), Scale(period));

    private TimeSpan Scale(TimeSpan span) => span == Timeout.InfiniteTimeSpan || span == TimeSpan.Zero ? span : span / Speed;
}
