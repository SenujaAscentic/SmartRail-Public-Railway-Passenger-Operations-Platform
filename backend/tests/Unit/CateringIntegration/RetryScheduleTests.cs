using SmartRail.CateringIntegration.Relay;
using Xunit;

namespace SmartRail.UnitTests.CateringIntegration;

// Contract 3.6: a webhook is retried after 10 s, 30 s, 1 min, 5 min, then every 15 min until 24 h after the first attempt; then Failed.
public class RetryScheduleTests
{
    private static readonly DateTime First = new(2026, 10, 20, 6, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 30)]
    [InlineData(3, 60)]
    [InlineData(4, 300)]
    [InlineData(5, 900)]
    [InlineData(6, 900)]
    [InlineData(40, 900)]
    public void The_wait_after_a_failed_attempt_follows_the_schedule_and_then_stays_at_15_minutes(int attemptsMade, int seconds)
    {
        var now = First.AddMinutes(1);

        Assert.Equal(now.AddSeconds(seconds), RetrySchedule.NextAttempt(attemptsMade, First, now));
    }

    [Fact]
    public void A_retry_that_would_come_more_than_24_hours_after_the_first_attempt_is_not_made_and_the_delivery_fails()
    {
        var lastPossible = First.AddHours(24).AddMinutes(-15);   // its retry would be exactly 24 h after the first attempt

        Assert.Equal(First.AddHours(24), RetrySchedule.NextAttempt(30, First, lastPossible));
        Assert.Null(RetrySchedule.NextAttempt(30, First, lastPossible.AddSeconds(1)));
    }

    [Fact]
    public void Following_the_schedule_from_the_first_failure_gives_up_after_about_a_hundred_attempts_inside_24_hours()
    {
        var now = First;
        var attempts = 0;
        while (true)
        {
            attempts++;
            if (RetrySchedule.NextAttempt(attempts, First, now) is not { } next)
            {
                break;
            }

            now = next;
        }

        Assert.InRange(now - First, TimeSpan.FromHours(23.5), TimeSpan.FromHours(24));
        Assert.InRange(attempts, 90, 100);   // 4 early ones, then one every 15 minutes
    }
}
