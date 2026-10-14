namespace SmartRail.LiveOperations.Domain;

// Data freshness of a journey's position (domain model 4.4).
public enum TrackingStatus { NotStarted, Live, Stale, Lost, Ended }

public static class TrackingFreshness
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan LostAfter = TimeSpan.FromMinutes(10);

    // R-06 for a journey that has been tracked: the status depends only on how old the last accepted position is.
    // Accepting a new position moves lastPositionAt to now, which gives Live again. NotStarted and Ended are
    // decided by journey start and end, not by time, so callers do not ask this function about them.
    public static TrackingStatus Of(DateTime lastPositionAt, DateTime now)
    {
        var age = now - lastPositionAt;
        if (age >= LostAfter) return TrackingStatus.Lost;
        if (age >= StaleAfter) return TrackingStatus.Stale;
        return TrackingStatus.Live;
    }
}
