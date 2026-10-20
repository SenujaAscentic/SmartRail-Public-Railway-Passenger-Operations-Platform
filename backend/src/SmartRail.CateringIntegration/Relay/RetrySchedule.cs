namespace SmartRail.CateringIntegration.Relay;

// When a failed webhook is tried again (contract 3.6): after 10 s, 30 s, 1 min and 5 min, then every 15 minutes until 24 hours after the
// first attempt; then the delivery is Failed. Pure (time is passed in), so it is unit-tested.
internal static class RetrySchedule
{
    private static readonly TimeSpan[] Early = [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)];

    private static readonly TimeSpan Then = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromHours(24);

    // `attemptsMade` counts the attempt that just failed (1 or more). Null: no further attempt, mark the delivery Failed.
    public static DateTime? NextAttempt(int attemptsMade, DateTime firstAttemptAt, DateTime now)
    {
        var wait = attemptsMade <= Early.Length ? Early[attemptsMade - 1] : Then;
        var next = now + wait;
        return next - firstAttemptAt > GiveUpAfter ? null : next;
    }
}
