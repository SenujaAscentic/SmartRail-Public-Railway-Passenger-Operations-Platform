namespace SmartRail.LiveOperations.Domain;

// Predicted times for the stops still ahead of the train.
public static class ExpectedTimes
{
    // R-02: scheduled + delay. A negative delay (running early) carries forward, so the prediction is earlier.
    public static DateTime Arrival(DateTime scheduledArrival, Delay delay) =>
        scheduledArrival.AddMinutes(delay.Minutes);

    // R-03: never earlier than scheduled, so an early train is still predicted to leave on time.
    // (An actual early departure is not predicted; it is recorded as it happened and Delay.Between shows it negative.)
    public static DateTime Departure(DateTime scheduledDeparture, Delay delay) =>
        scheduledDeparture.AddMinutes(Math.Max(0, delay.Minutes));

    // R-23: once now is past the expected arrival at the next stop, the train is at least (now - scheduled) late, even if
    // nothing has been reported. Never lower than the recorded delay, so it only grows while the train is overdue.
    public static Delay OverdueDelay(DateTime scheduledArrival, Delay recorded, DateTime now)
    {
        if (now <= Arrival(scheduledArrival, recorded))
        {
            return recorded;
        }

        var sinceScheduled = Delay.Between(scheduledArrival, now);   // whole minutes toward zero, as R-01
        return new Delay(Math.Max(recorded.Minutes, sinceScheduled.Minutes));
    }
}
