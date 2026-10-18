using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// R-14 and US-O4 AC4: cancel a Planned or Running journey (its remaining stops are skipped), and reinstate it before its scheduled start.
public class CancelReinstateTests
{
    private static readonly Guid S1 = Guid.NewGuid();   // origin, departs 06:00
    private static readonly Guid S2 = Guid.NewGuid();   // arrives 06:40, departs 06:42
    private static readonly Guid S3 = Guid.NewGuid();   // terminus, arrives 07:30
    private static readonly Guid Ruwan = Guid.NewGuid();
    private static readonly JourneyReason TrainFault = new("TrainFault", "Engine failure");

    private static DateTime At(int hour, int minute, int second = 0) => new(2026, 10, 14, hour, minute, second, DateTimeKind.Utc);

    private static OperationalJourney NewJourney() =>
        OperationalJourney.Plan(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 14), Guid.NewGuid(),
            [
                new PlannedStop(1, S1, CallType.Stop, null, 360, "1"),
                new PlannedStop(2, S2, CallType.Stop, 400, 402, "2"),
                new PlannedStop(3, S3, CallType.Stop, 450, null, "3"),
            ],
            TimeZoneInfo.Utc, Guid.NewGuid());

    private static JourneyStop Stop(OperationalJourney journey, Guid station) => journey.Stops.Single(s => s.StationId == station);

    [Fact]
    public void Cancel_marks_all_remaining_stops_skipped()
    {
        var journey = NewJourney();

        var result = journey.CancelByController(0, TrainFault, Ruwan, At(5, 0));

        Assert.Equal(DecisionResult.Done, result);
        Assert.Equal(JourneyStatus.Cancelled, journey.Status);
        Assert.All(journey.Stops, s => Assert.Equal(StopStatus.Skipped, s.Status));
        Assert.Equal(TrainFault, journey.Reason);
        Assert.Equal(1, journey.ControlVersion);
        Assert.Contains(new JourneyCancelled(journey.Id, TrainFault, At(5, 0)), journey.DomainEvents);
    }

    [Fact]
    public void Cancelling_a_running_train_keeps_what_has_already_happened()
    {
        var journey = NewJourney();
        journey.Start(At(5, 55), Guid.NewGuid(), Guid.NewGuid());
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 41));   // S1 inferred Departed, the train is standing at S2

        journey.CancelByController(0, TrainFault, Ruwan, At(6, 45));

        Assert.Equal(StopStatus.Departed, Stop(journey, S1).Status);
        Assert.Equal(StopStatus.Arrived, Stop(journey, S2).Status);    // the train is physically there
        Assert.Equal(StopStatus.Skipped, Stop(journey, S3).Status);
        Assert.Equal(JourneyStatus.Cancelled, journey.Status);
    }

    [Fact]
    public void A_cancelled_journey_cannot_be_started_and_its_tracking_ends()
    {
        var journey = NewJourney();
        journey.CancelByController(0, TrainFault, Ruwan, At(5, 0));

        Assert.Equal(StartResult.WrongState, journey.Start(At(5, 30), Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(TrackingStatus.Ended, journey.Tracking);
    }

    [Fact]
    public void Cancelling_releases_a_hold()
    {
        var journey = NewJourney();
        journey.SetHoldByController(0, S1, 10, null, Ruwan, At(5, 0));

        journey.CancelByController(1, TrainFault, Ruwan, At(5, 5));

        Assert.Null(journey.Hold);
        Assert.Contains(journey.DomainEvents, e => e is JourneyHeld { Released: true });
    }

    [Fact]
    public void A_completed_or_already_cancelled_journey_cannot_be_cancelled()
    {
        var done = NewJourney();
        done.Start(At(5, 30), Guid.NewGuid(), Guid.NewGuid());
        done.End(At(7, 30));
        Assert.Equal(DecisionResult.InvalidState, done.CancelByController(0, TrainFault, Ruwan, At(7, 31)));

        var cancelled = NewJourney();
        cancelled.CancelByController(0, TrainFault, Ruwan, At(5, 0));
        Assert.Equal(DecisionResult.InvalidState, cancelled.CancelByController(1, TrainFault, Ruwan, At(5, 1)));
        Assert.Equal(1, cancelled.ControlVersion);
    }

    [Fact]
    public void Cancel_on_a_stale_version_is_refused_and_changes_nothing()
    {
        var journey = NewJourney();

        Assert.Equal(DecisionResult.StaleVersion, journey.CancelByController(3, TrainFault, Ruwan, At(5, 0)));
        Assert.Equal(JourneyStatus.Planned, journey.Status);
        Assert.All(journey.Stops, s => Assert.Equal(StopStatus.Scheduled, s.Status));
    }

    [Fact]
    public void Reinstating_puts_the_journey_back_as_planned_with_its_stops_scheduled_and_no_reason()
    {
        var journey = NewJourney();
        journey.CancelByController(0, TrainFault, Ruwan, At(5, 0));
        journey.ClearDomainEvents();

        var result = journey.ReinstateByController(1, Ruwan, At(5, 20));

        Assert.Equal(DecisionResult.Done, result);
        Assert.Equal(JourneyStatus.Planned, journey.Status);
        Assert.All(journey.Stops, s => Assert.Equal(StopStatus.Scheduled, s.Status));
        Assert.Null(journey.Reason);
        Assert.Equal(TrackingStatus.NotStarted, journey.Tracking);
        Assert.Equal(2, journey.ControlVersion);
        Assert.Contains(new JourneyReinstated(journey.Id, At(5, 20)), journey.DomainEvents);
        Assert.Equal(StartResult.Started, journey.Start(At(5, 30), Guid.NewGuid(), Guid.NewGuid()));   // and it can run again
    }

    [Fact]
    public void Reinstate_refused_after_scheduled_start()
    {
        var journey = NewJourney();
        journey.CancelByController(0, TrainFault, Ruwan, At(5, 0));

        Assert.Equal(DecisionResult.InvalidState, journey.ReinstateByController(1, Ruwan, At(6, 0)));      // exactly at the scheduled start
        Assert.Equal(DecisionResult.InvalidState, journey.ReinstateByController(1, Ruwan, At(6, 30)));
        Assert.Equal(JourneyStatus.Cancelled, journey.Status);
        Assert.Equal(1, journey.ControlVersion);
    }

    [Fact]
    public void Only_a_cancelled_journey_can_be_reinstated()
    {
        var journey = NewJourney();

        Assert.Equal(DecisionResult.InvalidState, journey.ReinstateByController(0, Ruwan, At(5, 0)));
    }

    [Fact]
    public void Reinstate_on_a_stale_version_is_refused()
    {
        var journey = NewJourney();
        journey.CancelByController(0, TrainFault, Ruwan, At(5, 0));

        Assert.Equal(DecisionResult.StaleVersion, journey.ReinstateByController(0, Ruwan, At(5, 5)));
    }

    [Fact]
    public void A_skipped_stop_is_not_the_next_stop_for_the_overdue_delay()
    {
        var journey = NewJourney();
        journey.Start(At(5, 55), Guid.NewGuid(), Guid.NewGuid());
        journey.RecordPosition(new PositionSample(new GeoPoint(7.0, 80.0), 40, At(6, 0), At(6, 0), TimeSpan.Zero, 0, new Dictionary<Guid, double>()), At(6, 0));
        journey.CancelByController(0, TrainFault, Ruwan, At(6, 5));

        journey.RefreshTracking(At(8, 0));   // two hours past every scheduled time

        Assert.Equal(0, journey.DelayMinutes);   // a cancelled train is not "late"
    }
}
