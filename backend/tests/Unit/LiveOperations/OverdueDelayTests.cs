using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// R-23: a train that has not reached its next stop after its expected arrival is late by at least now - scheduled.
public class OverdueDelayTests
{
    private static readonly Guid S1 = Guid.NewGuid();   // origin, departs 06:00
    private static readonly Guid S2 = Guid.NewGuid();   // arrives 06:40, departs 06:42
    private static readonly Guid S3 = Guid.NewGuid();   // terminus, arrives 07:30

    private static DateTime At(int hour, int minute, int second = 0) =>
        new(2026, 10, 14, hour, minute, second, DateTimeKind.Utc);

    private static OperationalJourney NewJourney() =>
        OperationalJourney.Plan(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 14), Guid.NewGuid(),
            [
                new PlannedStop(1, S1, CallType.Stop, null, 360, "1"),
                new PlannedStop(2, S2, CallType.Stop, 400, 402, "2"),
                new PlannedStop(3, S3, CallType.Stop, 450, null, "3"),
            ],
            TimeZoneInfo.Utc, Guid.NewGuid());

    // A tracked journey: one accepted position, far from every station.
    private static OperationalJourney TrackedJourney()
    {
        var journey = NewJourney();
        var time = At(6, 30);
        journey.RecordPosition(
            new PositionSample(new GeoPoint(7.0, 80.0), 60, time, time, TimeSpan.Zero, 0, new Dictionary<Guid, double>()), time);
        journey.ClearDomainEvents();
        return journey;
    }

    private static PositionSample Near(DateTime time, double speedKmh, Guid station, double metres) =>
        new(new GeoPoint(7.0, 80.0), speedKmh, time, time, TimeSpan.Zero, 0, new Dictionary<Guid, double> { [station] = metres });

    // ---- The pure rule --------------------------------------------------------------------------------------

    private static readonly DateTime Scheduled = At(6, 40);

    [Fact]
    public void Delay_is_unchanged_until_now_passes_the_expected_arrival()
    {
        // Recorded 4 min late: expected 06:44. At exactly 06:44 it is not yet overdue.
        Assert.Equal(new Delay(4), ExpectedTimes.OverdueDelay(Scheduled, new Delay(4), At(6, 44)));
        Assert.Equal(new Delay(0), ExpectedTimes.OverdueDelay(Scheduled, new Delay(0), At(6, 39)));
    }

    [Fact]
    public void Delay_grows_in_whole_minutes_once_the_expected_arrival_has_passed()
    {
        Assert.Equal(new Delay(3), ExpectedTimes.OverdueDelay(Scheduled, new Delay(0), At(6, 43, 59)));   // toward zero
        Assert.Equal(new Delay(4), ExpectedTimes.OverdueDelay(Scheduled, new Delay(0), At(6, 44)));
        Assert.Equal(new Delay(25), ExpectedTimes.OverdueDelay(Scheduled, new Delay(0), At(7, 5)));
    }

    [Fact]
    public void Overdue_delay_never_lowers_the_recorded_delay()
    {
        // Expected 06:50; at 06:45 it is not overdue. At 06:51 the overdue value (11) is above the recorded 10.
        Assert.Equal(new Delay(10), ExpectedTimes.OverdueDelay(Scheduled, new Delay(10), At(6, 45)));
        Assert.Equal(new Delay(11), ExpectedTimes.OverdueDelay(Scheduled, new Delay(10), At(6, 51)));
        // Overdue by seconds only: now - scheduled is 4, recorded is 4, so it stays 4.
        Assert.Equal(new Delay(4), ExpectedTimes.OverdueDelay(Scheduled, new Delay(4), At(6, 44, 30)));
    }

    // The key case: a known delay of 10 must not drop to 1 just because now is only 1 min after the scheduled time.
    [Fact]
    public void Overdue_delay_never_drops_below_a_known_delay_just_after_the_scheduled_time()
    {
        Assert.Equal(new Delay(10), ExpectedTimes.OverdueDelay(Scheduled, new Delay(10), Scheduled.AddMinutes(1)));
    }

    [Fact]
    public void Overdue_delay_moves_an_early_train_back_towards_schedule()
    {
        // Recorded 2 min early: expected 06:38. At 06:39 the train is overdue and can only be 1 min early.
        Assert.Equal(new Delay(-1), ExpectedTimes.OverdueDelay(Scheduled, new Delay(-2), At(6, 39)));
    }

    // ---- In the journey -------------------------------------------------------------------------------------

    [Fact]
    public void Train_stuck_between_stations_shows_growing_lateness()
    {
        var journey = TrackedJourney();

        journey.RefreshTracking(At(6, 40, 30));   // overdue by 30 s: still 0 whole minutes
        Assert.Equal(0, journey.DelayMinutes);
        Assert.DoesNotContain(journey.DomainEvents, e => e is DelayChanged);

        journey.RefreshTracking(At(6, 41));
        Assert.Equal(1, journey.DelayMinutes);

        journey.RefreshTracking(At(6, 42));
        Assert.Equal(2, journey.DelayMinutes);

        Assert.Equal(
            [new DelayChanged(journey.Id, 1, 0, Source.Schedule, At(6, 41)), new DelayChanged(journey.Id, 2, 1, Source.Schedule, At(6, 42))],
            journey.DomainEvents.OfType<DelayChanged>());
    }

    [Fact]
    public void Known_delay_of_10_stays_10_one_minute_after_the_next_stops_scheduled_time()
    {
        var journey = TrackedJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 50));   // known: 10 min late
        journey.ClearDomainEvents();

        journey.RefreshTracking(At(7, 31));   // S3 was scheduled 07:30; expected 07:40

        Assert.Equal(10, journey.DelayMinutes);
        Assert.DoesNotContain(journey.DomainEvents, e => e is DelayChanged);
    }

    [Fact]
    public void Refreshing_twice_at_the_same_minute_announces_the_delay_once()
    {
        var journey = TrackedJourney();

        journey.RefreshTracking(At(6, 45));
        journey.RefreshTracking(At(6, 45, 20));

        Assert.Single(journey.DomainEvents.OfType<DelayChanged>());
    }

    [Fact]
    public void Overdue_delay_uses_the_next_stop_not_yet_reached()
    {
        var journey = TrackedJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 40));
        journey.RecordCrewTime(S2, StopEventKind.Departure, At(6, 42));

        journey.RefreshTracking(At(7, 35));   // S3 was due 07:30

        Assert.Equal(5, journey.DelayMinutes);
    }

    [Fact]
    public void Overdue_delay_skips_a_stop_the_train_has_passed()
    {
        var journey = TrackedJourney();
        journey.RecordPosition(Near(At(6, 41), 80, S2, 100), At(6, 41));
        journey.RecordPosition(Near(At(6, 41, 20), 80, S2, 500), At(6, 41, 20));   // passed S2, 1 min late
        Assert.Equal(1, journey.DelayMinutes);

        journey.RefreshTracking(At(6, 55));   // S2 would be 15 min overdue if it still counted

        Assert.Equal(1, journey.DelayMinutes);
    }

    [Fact]
    public void No_overdue_delay_for_a_journey_that_has_not_started()
    {
        var journey = NewJourney();   // no position ever accepted: "Not started" (domain model 4.2)

        journey.RefreshTracking(At(7, 0));

        Assert.Equal(0, journey.DelayMinutes);
        Assert.Empty(journey.DomainEvents);
    }

    [Fact]
    public void Controller_delay_takes_precedence_over_overdue_delay()
    {
        var journey = TrackedJourney();
        journey.SetControllerDelay(new Delay(3), At(6, 35));
        journey.ClearDomainEvents();

        journey.RefreshTracking(At(6, 55));

        Assert.Equal(new SourcedDelay(new Delay(3), Source.Controller), journey.CurrentDelay);
        Assert.DoesNotContain(journey.DomainEvents, e => e is DelayChanged);
    }

    [Fact]
    public void Real_arrival_replaces_the_overdue_delay()
    {
        var journey = TrackedJourney();
        journey.RefreshTracking(At(6, 47));   // overdue: 7 min late
        Assert.Equal(7, journey.DelayMinutes);

        journey.RecordPosition(Near(At(6, 44), 3, S2, 100), At(6, 48));   // a (late-delivered) stop at 06:44
        journey.RecordPosition(Near(At(6, 44, 20), 0, S2, 50), At(6, 48, 1));

        Assert.Equal(new SourcedDelay(new Delay(4), Source.Gps), journey.CurrentDelay);
    }

    // The overdue value carries the lowest priority, so it must not lock out a real report.
    [Fact]
    public void Real_report_can_lower_an_overdue_delay_that_replaced_a_crew_delay()
    {
        var journey = TrackedJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 44));   // 4 min late, source Crew
        journey.RefreshTracking(At(7, 40));                              // S3 due 07:34: overdue, 10 min late
        Assert.Equal(10, journey.DelayMinutes);

        journey.RecordPosition(Near(At(7, 36), 3, S3, 100), At(7, 41));
        journey.RecordPosition(Near(At(7, 36, 20), 0, S3, 50), At(7, 41, 1));   // GPS says it stopped at 07:36: 6 late

        Assert.Equal(new SourcedDelay(new Delay(6), Source.Gps), journey.CurrentDelay);
    }
}
