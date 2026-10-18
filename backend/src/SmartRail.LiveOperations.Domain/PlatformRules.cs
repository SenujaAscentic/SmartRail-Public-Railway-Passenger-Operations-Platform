namespace SmartRail.LiveOperations.Domain;

// R-11: how much notice the passengers get of a platform change.
public enum LeadTimeLevel { Normal, ShortNotice, Late }

// The notice for a platform change, and what the controller is told about it. `Late` does not block the change: it is a warning, and
// a hold is recommended (US-O5 AC5, approved 18 Oct). The "train will wait" promise is made only when a hold is really set.
public sealed record PlatformLeadTime(TimeSpan LeadTime, LeadTimeLevel Level)
{
    // Whole minutes, rounded down (negative when the train is already due).
    public int Minutes => (int)Math.Floor(LeadTime.TotalMinutes);

    public bool HoldRecommended => Level == LeadTimeLevel.Late;
}

public static class PlatformRules
{
    // R-11 (M-1, 12 Oct): lead time = expected ARRIVAL minus now; at the origin, where there is no arrival, the expected departure.
    // More than 15 minutes is Normal, 5 to 15 (both included) ShortNotice, less than 5 Late.
    public static PlatformLeadTime LeadTime(DateTime? expectedArrival, DateTime? expectedDeparture, DateTime now)
    {
        var due = expectedArrival ?? expectedDeparture ?? throw new ArgumentException("The stop has neither an arrival nor a departure.");
        var lead = due - now;
        var level = lead > TimeSpan.FromMinutes(15) ? LeadTimeLevel.Normal
            : lead >= TimeSpan.FromMinutes(5) ? LeadTimeLevel.ShortNotice
            : LeadTimeLevel.Late;
        return new PlatformLeadTime(lead, level);
    }

    public static readonly TimeSpan ConflictWindow = TimeSpan.FromMinutes(5);

    // R-12: another train is expected at the same platform within 5 minutes of this train's time there. Each train occupies the
    // platform from its arrival to its departure (a stop with only one time is a point in time). Two trains conflict when, after
    // widening one of them by 5 minutes, they overlap. The caller has already chosen only trains at the same station and platform.
    public static bool Conflicts(DateTime thisStart, DateTime? thisEnd, DateTime otherStart, DateTime? otherEnd) =>
        thisStart - ConflictWindow <= (otherEnd ?? otherStart) && otherStart - ConflictWindow <= (thisEnd ?? thisStart);
}
