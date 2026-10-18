using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// R-14 and US-O4: terminate a running journey early at a station ahead of the train. The stops after it are skipped; the journey ends.
public class TerminateTests
{
    private static readonly Guid S1 = Guid.NewGuid();   // origin, departs 06:00
    private static readonly Guid S2 = Guid.NewGuid();   // arrives 06:40, departs 06:42
    private static readonly Guid S3 = Guid.NewGuid();   // arrives 07:00, departs 07:02
    private static readonly Guid S4 = Guid.NewGuid();   // terminus, arrives 07:30
    private static readonly Guid Ruwan = Guid.NewGuid();
    private static readonly JourneyReason Fault = new("TrainFault", "Engine failure");

    private static DateTime At(int hour, int minute) => new(2026, 10, 14, hour, minute, 0, DateTimeKind.Utc);

    private static OperationalJourney RunningJourney()
    {
        var journey = OperationalJourney.Plan(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 14), Guid.NewGuid(),
            [
                new PlannedStop(1, S1, CallType.Stop, null, 360, "1"),
                new PlannedStop(2, S2, CallType.Stop, 400, 402, "2"),
                new PlannedStop(3, S3, CallType.Stop, 420, 422, "3"),
                new PlannedStop(4, S4, CallType.Stop, 450, null, "4"),
            ],
            TimeZoneInfo.Utc, Guid.NewGuid());
        journey.Start(At(5, 55), Guid.NewGuid(), Guid.NewGuid());
        journey.RecordCrewTime(S1, StopEventKind.Departure, At(6, 0));
        journey.ClearDomainEvents();
        return journey;
    }

    private static StopStatus StatusOf(OperationalJourney journey, Guid station) => journey.Stops.Single(s => s.StationId == station).Status;

    [Fact]
    public void Terminating_at_a_station_ahead_skips_the_stops_after_it_and_ends_the_journey()
    {
        var journey = RunningJourney();

        var result = journey.TerminateByController(0, S3, Fault, Ruwan, At(6, 20));

        Assert.Equal(DecisionResult.Done, result);
        Assert.Equal(JourneyStatus.TerminatedEarly, journey.Status);
        Assert.Equal(StopStatus.Scheduled, StatusOf(journey, S2));   // still to be called at
        Assert.Equal(StopStatus.Scheduled, StatusOf(journey, S3));   // the last stop now
        Assert.Equal(StopStatus.Skipped, StatusOf(journey, S4));
        Assert.Equal(Fault, journey.Reason);
        Assert.Equal(TrackingStatus.Ended, journey.Tracking);
        Assert.Equal(1, journey.ControlVersion);
        Assert.Contains(new JourneyTerminatedEarly(journey.Id, S3, Fault, At(6, 20)), journey.DomainEvents);
        Assert.Contains(new JourneyEnded(journey.Id, At(6, 20), JourneyEndReason.Controller), journey.DomainEvents);
    }

    [Fact]
    public void Terminating_at_the_station_the_train_is_standing_at_is_allowed()
    {
        var journey = RunningJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 41));

        Assert.Equal(DecisionResult.Done, journey.TerminateByController(0, S2, Fault, Ruwan, At(6, 42)));
        Assert.Equal(StopStatus.Arrived, StatusOf(journey, S2));
        Assert.Equal(StopStatus.Skipped, StatusOf(journey, S3));
        Assert.Equal(StopStatus.Skipped, StatusOf(journey, S4));
    }

    [Fact]
    public void Terminate_refused_for_station_already_passed()
    {
        var journey = RunningJourney();
        journey.RecordCrewTime(S2, StopEventKind.Departure, At(6, 43));

        Assert.Equal(DecisionResult.InvalidState, journey.TerminateByController(0, S2, Fault, Ruwan, At(6, 44)));
        Assert.Equal(DecisionResult.InvalidState, journey.TerminateByController(0, S1, Fault, Ruwan, At(6, 44)));
        Assert.Equal(JourneyStatus.Running, journey.Status);
        Assert.Equal(0, journey.ControlVersion);
    }

    [Fact]
    public void Only_a_running_journey_can_be_terminated()
    {
        var planned = OperationalJourney.Plan(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 14), Guid.NewGuid(),
            [new PlannedStop(1, S1, CallType.Stop, null, 360, "1"), new PlannedStop(2, S2, CallType.Stop, 400, null, "2")], TimeZoneInfo.Utc, Guid.NewGuid());
        Assert.Equal(DecisionResult.InvalidState, planned.TerminateByController(0, S2, Fault, Ruwan, At(5, 0)));

        var cancelled = RunningJourney();
        cancelled.CancelByController(0, Fault, Ruwan, At(6, 10));
        Assert.Equal(DecisionResult.InvalidState, cancelled.TerminateByController(1, S3, Fault, Ruwan, At(6, 11)));
    }

    [Fact]
    public void Terminating_at_a_station_that_is_not_a_stop_is_refused()
    {
        var journey = RunningJourney();

        Assert.Equal(DecisionResult.UnknownStop, journey.TerminateByController(0, Guid.NewGuid(), Fault, Ruwan, At(6, 20)));
    }

    [Fact]
    public void Terminate_on_a_stale_version_is_refused_and_changes_nothing()
    {
        var journey = RunningJourney();

        Assert.Equal(DecisionResult.StaleVersion, journey.TerminateByController(5, S3, Fault, Ruwan, At(6, 20)));
        Assert.Equal(JourneyStatus.Running, journey.Status);
        Assert.Equal(StopStatus.Scheduled, StatusOf(journey, S4));
    }

    [Fact]
    public void Terminating_releases_a_hold()
    {
        var journey = RunningJourney();
        journey.SetHoldByController(0, S2, 10, null, Ruwan, At(6, 10));

        journey.TerminateByController(1, S3, Fault, Ruwan, At(6, 15));

        Assert.Null(journey.Hold);
    }

    [Fact]
    public void A_terminated_journey_cannot_be_ended_again_by_the_driver_or_changed_by_the_controller()
    {
        var journey = RunningJourney();
        journey.TerminateByController(0, S3, Fault, Ruwan, At(6, 20));

        Assert.Equal(EndResult.WrongState, journey.End(At(7, 0)));
        Assert.Equal(DecisionResult.InvalidState, journey.SetDelayByController(1, new Delay(5), Fault, Ruwan, At(6, 30)));
        Assert.Equal(DecisionResult.InvalidState, journey.CancelByController(1, Fault, Ruwan, At(6, 30)));
    }
}
