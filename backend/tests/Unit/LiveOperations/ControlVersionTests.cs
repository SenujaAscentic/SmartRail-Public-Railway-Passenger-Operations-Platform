using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// R-18 and the controller delay (US-O4): only decisions bump the control version, a decision based on an old version is refused,
// and the delay a controller sets carries its reason. Written before the code (ADR-08).
public class ControlVersionTests
{
    private static readonly Guid S1 = Guid.NewGuid();   // origin, departs 06:00
    private static readonly Guid S2 = Guid.NewGuid();   // arrives 06:40, departs 06:42
    private static readonly Guid S3 = Guid.NewGuid();   // terminus, arrives 07:30
    private static readonly Guid Ruwan = Guid.NewGuid();
    private static readonly Guid Chamari = Guid.NewGuid();
    private static readonly JourneyReason SignalFault = new("SignalFailure", "Signal fault at Ragama");

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

    private static PositionSample Fix(DateTime time, double speedKmh, Guid station, double metres) =>
        new(new GeoPoint(7.0, 80.0), speedKmh, time, time, TimeSpan.Zero, 0, new Dictionary<Guid, double> { [station] = metres });

    // ---- the version ----

    [Fact]
    public void A_new_journey_has_control_version_0()
    {
        Assert.Equal(0, NewJourney().ControlVersion);
    }

    [Fact]
    public void A_decision_bumps_the_version_by_one_and_remembers_who_and_when()
    {
        var journey = NewJourney();

        var result = journey.SetDelayByController(expectedVersion: 0, new Delay(10), SignalFault, Ruwan, At(5, 0));

        Assert.Equal(DecisionResult.Done, result);
        Assert.Equal(1, journey.ControlVersion);
        Assert.Equal((Ruwan, At(5, 0)), (journey.LastDecisionBy, journey.LastDecisionAt));
    }

    [Fact]
    public void Action_refused_when_journey_version_is_stale()
    {
        var journey = NewJourney();
        journey.SetDelayByController(0, new Delay(10), SignalFault, Ruwan, At(5, 0));
        journey.ClearDomainEvents();

        // Chamari looked at the journey when it was at version 0 and now acts on that.
        var result = journey.SetDelayByController(expectedVersion: 0, new Delay(20), SignalFault, Chamari, At(5, 1));

        Assert.Equal(DecisionResult.StaleVersion, result);
        Assert.Equal(1, journey.ControlVersion);
        Assert.Equal(10, journey.DelayMinutes);          // nothing changed
        Assert.Equal(Ruwan, journey.LastDecisionBy);     // so the refusal can say who got there first
        Assert.Empty(journey.DomainEvents);
    }

    [Fact]
    public void A_version_from_the_future_is_refused_too()
    {
        var journey = NewJourney();

        Assert.Equal(DecisionResult.StaleVersion, journey.SetDelayByController(3, new Delay(10), SignalFault, Ruwan, At(5, 0)));
        Assert.Equal(0, journey.ControlVersion);
    }

    [Fact]
    public void Gps_update_does_not_invalidate_controller_version()
    {
        var journey = NewJourney();

        // A train arriving at S2 four minutes late, detected by GPS, then the periodic refresh.
        journey.RecordPosition(Fix(At(6, 43, 50), 40, S2, 400), At(6, 43, 50));
        journey.RecordPosition(Fix(At(6, 44, 0), 3, S2, 100), At(6, 44, 0));
        journey.RecordPosition(Fix(At(6, 44, 20), 0, S2, 50), At(6, 44, 20));
        journey.RefreshTracking(At(6, 50));

        Assert.Equal(0, journey.ControlVersion);
        Assert.Equal(DecisionResult.Done, journey.SetDelayByController(0, new Delay(10), SignalFault, Ruwan, At(6, 51)));
    }

    [Fact]
    public void Crew_tap_does_not_invalidate_controller_version()
    {
        var journey = NewJourney();

        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 45));

        Assert.Equal(0, journey.ControlVersion);
        Assert.Equal(DecisionResult.Done, journey.SetDelayByController(0, new Delay(10), SignalFault, Ruwan, At(6, 46)));
    }

    // ---- the controller delay ----

    [Fact]
    public void A_controller_delay_sets_the_delay_with_source_Controller_and_announces_it_with_its_reason()
    {
        var journey = NewJourney();

        journey.SetDelayByController(0, new Delay(15), SignalFault, Ruwan, At(5, 0));

        Assert.Equal(new SourcedDelay(new Delay(15), Source.Controller), journey.CurrentDelay);
        Assert.Equal(SignalFault, journey.Reason);
        Assert.Contains(new DelayChanged(journey.Id, 15, 0, Source.Controller, At(5, 0), SignalFault), journey.DomainEvents);
    }

    [Fact]
    public void A_controller_delay_is_announced_with_its_reason_even_when_the_minutes_do_not_change()
    {
        var journey = NewJourney();
        journey.SetDelayByController(0, new Delay(0), new JourneyReason("Weather", null), Ruwan, At(5, 0));

        // Same minutes, but the reason is news for the passengers.
        Assert.Contains(journey.DomainEvents, e => e is DelayChanged { DelayMinutes: 0, PreviousDelayMinutes: 0, Reason.Code: "Weather" });
    }

    [Fact]
    public void A_controller_delay_is_kept_when_a_crew_tap_or_gps_arrives_later()
    {
        var journey = NewJourney();
        journey.SetDelayByController(0, new Delay(15), SignalFault, Ruwan, At(6, 0));

        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 45));

        Assert.Equal(new SourcedDelay(new Delay(15), Source.Controller), journey.CurrentDelay);   // R-05
    }

    [Fact]
    public void Clearing_the_controller_delay_goes_back_to_the_delay_of_the_latest_stop_event_and_drops_the_reason()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 45));   // 5 min late
        journey.SetDelayByController(0, new Delay(15), SignalFault, Ruwan, At(6, 46));

        var result = journey.ClearDelayByController(expectedVersion: 1, Ruwan, At(6, 50));

        Assert.Equal(DecisionResult.Done, result);
        Assert.Equal(new SourcedDelay(new Delay(5), Source.Crew), journey.CurrentDelay);   // not a false "on time" (US-P4)
        Assert.Null(journey.Reason);
        Assert.Equal(2, journey.ControlVersion);
    }

    [Fact]
    public void Clearing_when_no_controller_delay_is_set_is_refused_and_changes_nothing()
    {
        var journey = NewJourney();

        Assert.Equal(DecisionResult.InvalidState, journey.ClearDelayByController(0, Ruwan, At(5, 0)));
        Assert.Equal(0, journey.ControlVersion);
    }

    [Fact]
    public void A_delay_cannot_be_set_on_a_completed_journey()
    {
        var journey = NewJourney();
        journey.Start(At(5, 30), Guid.NewGuid(), Guid.NewGuid());
        journey.End(At(7, 30));

        Assert.Equal(DecisionResult.InvalidState, journey.SetDelayByController(0, new Delay(10), SignalFault, Ruwan, At(7, 31)));
        Assert.Equal(0, journey.ControlVersion);
    }

    [Fact]
    public void A_stale_version_is_reported_before_a_wrong_state()
    {
        var journey = NewJourney();
        journey.Start(At(5, 30), Guid.NewGuid(), Guid.NewGuid());
        journey.End(At(7, 30));

        // Stale AND completed: the person is told to look again first.
        Assert.Equal(DecisionResult.StaleVersion, journey.SetDelayByController(9, new Delay(10), SignalFault, Ruwan, At(7, 31)));
    }
}
