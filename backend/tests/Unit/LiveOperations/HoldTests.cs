using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// US-O4 and R-22: the controller holds a train at a station and releases it; while it is held its expected departure moves with the clock.
public class HoldTests
{
    private static readonly Guid S1 = Guid.NewGuid();   // origin, departs 06:00
    private static readonly Guid S2 = Guid.NewGuid();   // arrives 06:40, departs 06:42
    private static readonly Guid S3 = Guid.NewGuid();   // terminus, arrives 07:30
    private static readonly Guid Ruwan = Guid.NewGuid();
    private static readonly JourneyReason Waiting = new("Congestion", "Waiting for the platform");

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

    // A running train that has just been seen (tracking Live), so the periodic refresh does its work.
    private static OperationalJourney TrackedJourneyAtS2()
    {
        var journey = NewJourney();
        journey.Start(At(5, 55), Guid.NewGuid(), Guid.NewGuid());
        journey.RecordPosition(new PositionSample(new GeoPoint(7.0, 80.0), 40, At(6, 40), At(6, 40), TimeSpan.Zero, 0, new Dictionary<Guid, double>()), At(6, 40));
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 40));
        journey.ClearDomainEvents();
        return journey;
    }

    [Fact]
    public void A_hold_is_set_at_a_station_the_train_has_not_left_and_is_announced()
    {
        var journey = TrackedJourneyAtS2();

        var result = journey.SetHoldByController(0, S2, 10, Waiting, Ruwan, At(6, 41));

        Assert.Equal(DecisionResult.Done, result);
        Assert.Equal(new JourneyHold(S2, 10, Waiting, At(6, 41)), journey.Hold);
        Assert.Equal(1, journey.ControlVersion);
        Assert.Contains(new JourneyHeld(journey.Id, S2, Released: false, 10, Waiting, Source.Controller, At(6, 41)), journey.DomainEvents);
    }

    [Fact]
    public void A_hold_needs_no_reason()
    {
        var journey = TrackedJourneyAtS2();

        Assert.Equal(DecisionResult.Done, journey.SetHoldByController(0, S2, 5, reason: null, Ruwan, At(6, 41)));
        Assert.Null(journey.Hold!.Reason);
    }

    [Fact]
    public void A_hold_at_a_station_that_is_not_a_stop_of_the_journey_is_refused()
    {
        var journey = TrackedJourneyAtS2();

        Assert.Equal(DecisionResult.UnknownStop, journey.SetHoldByController(0, Guid.NewGuid(), 10, Waiting, Ruwan, At(6, 41)));
        Assert.Equal(0, journey.ControlVersion);
    }

    [Fact]
    public void A_hold_at_a_station_the_train_has_already_left_is_refused()
    {
        var journey = TrackedJourneyAtS2();
        journey.RecordCrewTime(S2, StopEventKind.Departure, At(6, 43));

        Assert.Equal(DecisionResult.InvalidState, journey.SetHoldByController(0, S2, 10, Waiting, Ruwan, At(6, 44)));
        Assert.Equal(DecisionResult.InvalidState, journey.SetHoldByController(0, S1, 10, Waiting, Ruwan, At(6, 44)));   // inferred Departed
    }

    [Fact]
    public void A_hold_at_the_terminus_is_refused_because_there_is_no_departure_to_hold()
    {
        var journey = TrackedJourneyAtS2();

        Assert.Equal(DecisionResult.InvalidState, journey.SetHoldByController(0, S3, 10, Waiting, Ruwan, At(6, 44)));
    }

    [Fact]
    public void A_second_hold_is_refused_while_one_is_set()
    {
        var journey = TrackedJourneyAtS2();
        journey.SetHoldByController(0, S2, 10, Waiting, Ruwan, At(6, 41));

        Assert.Equal(DecisionResult.InvalidState, journey.SetHoldByController(1, S2, 20, Waiting, Ruwan, At(6, 42)));
        Assert.Equal(1, journey.ControlVersion);
    }

    [Fact]
    public void A_hold_on_a_stale_version_is_refused()
    {
        var journey = TrackedJourneyAtS2();

        Assert.Equal(DecisionResult.StaleVersion, journey.SetHoldByController(4, S2, 10, Waiting, Ruwan, At(6, 41)));
        Assert.Null(journey.Hold);
    }

    [Fact]
    public void Releasing_removes_the_hold_announces_it_and_counts_as_a_decision()
    {
        var journey = TrackedJourneyAtS2();
        journey.SetHoldByController(0, S2, 10, Waiting, Ruwan, At(6, 41));
        journey.ClearDomainEvents();

        var result = journey.ReleaseHoldByController(1, Ruwan, At(6, 44));

        Assert.Equal(DecisionResult.Done, result);
        Assert.Null(journey.Hold);
        Assert.Equal(2, journey.ControlVersion);
        Assert.Contains(new JourneyHeld(journey.Id, S2, Released: true, null, null, Source.Controller, At(6, 44)), journey.DomainEvents);
    }

    [Fact]
    public void Releasing_when_nothing_is_held_is_refused()
    {
        var journey = TrackedJourneyAtS2();

        Assert.Equal(DecisionResult.InvalidState, journey.ReleaseHoldByController(0, Ruwan, At(6, 44)));
        Assert.Equal(0, journey.ControlVersion);
    }

    [Fact]
    public void Held_train_expected_departure_moves_forward_with_the_clock()
    {
        var journey = TrackedJourneyAtS2();
        journey.SetHoldByController(0, S2, 30, Waiting, Ruwan, At(6, 41));
        var s2 = journey.Stops.Single(s => s.StationId == S2);

        journey.RefreshTracking(At(6, 50));
        Assert.Equal(At(6, 50), s2.ExpectedDeparture(journey.CurrentDelay.Value));   // scheduled 06:42, but the train is still here at 06:50

        journey.RefreshTracking(At(6, 58));
        Assert.Equal(At(6, 58), s2.ExpectedDeparture(journey.CurrentDelay.Value));
        Assert.Equal(16, journey.DelayMinutes);   // later stops move with it (R-02)
    }

    [Fact]
    public void A_hold_before_the_scheduled_departure_changes_nothing_until_that_time_passes()
    {
        var journey = TrackedJourneyAtS2();
        journey.SetHoldByController(0, S2, 30, Waiting, Ruwan, At(6, 41));

        journey.RefreshTracking(At(6, 41, 30));

        Assert.Equal(0, journey.DelayMinutes);   // expected departure = max(scheduled, now) = scheduled 06:42
    }

    [Fact]
    public void The_hold_ends_by_itself_when_the_train_departs_and_that_is_not_a_decision()
    {
        var journey = TrackedJourneyAtS2();
        journey.SetHoldByController(0, S2, 30, Waiting, Ruwan, At(6, 41));
        journey.ClearDomainEvents();

        journey.RecordCrewTime(S2, StopEventKind.Departure, At(6, 52));

        Assert.Null(journey.Hold);
        Assert.Equal(1, journey.ControlVersion);   // R-18: only decisions count
        Assert.Contains(journey.DomainEvents, e => e is JourneyHeld { Released: true, Source: Source.Crew });
        Assert.Equal(10, journey.DelayMinutes);    // measured from the actual departure (R-22)
    }

    [Fact]
    public void A_hold_cannot_be_set_on_a_completed_journey()
    {
        var journey = TrackedJourneyAtS2();
        journey.End(At(7, 30));

        Assert.Equal(DecisionResult.InvalidState, journey.SetHoldByController(0, S2, 10, Waiting, Ruwan, At(7, 31)));
    }
}
