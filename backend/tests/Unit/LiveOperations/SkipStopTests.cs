using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// R-15 and R-21 (D-6, D-11): the controller skips a stop; the later stops gain the stop's scheduled dwell, never below on time.
public class SkipStopTests
{
    private static readonly Guid S1 = Guid.NewGuid();   // origin, departs 06:00
    private static readonly Guid S2 = Guid.NewGuid();   // arrives 06:40, departs 06:42 (dwell 2)
    private static readonly Guid S3 = Guid.NewGuid();   // arrives 07:00, departs 07:05 (dwell 5)
    private static readonly Guid S4 = Guid.NewGuid();   // arrives 07:30, departs 07:31 (dwell 1)
    private static readonly Guid S5 = Guid.NewGuid();   // terminus, arrives 08:00
    private static readonly Guid Ruwan = Guid.NewGuid();

    private static DateTime At(int hour, int minute) => new(2026, 10, 14, hour, minute, 0, DateTimeKind.Utc);

    private static OperationalJourney NewJourney(bool withPass = false) =>
        OperationalJourney.Plan(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 14), Guid.NewGuid(),
            [
                new PlannedStop(1, S1, CallType.Stop, null, 360, "1"),
                new PlannedStop(2, S2, withPass ? CallType.Pass : CallType.Stop, 400, 402, "2"),
                new PlannedStop(3, S3, CallType.Stop, 420, 425, "3"),
                new PlannedStop(4, S4, CallType.Stop, 450, 451, "4"),
                new PlannedStop(5, S5, CallType.Stop, 480, null, "5"),
            ],
            TimeZoneInfo.Utc, Guid.NewGuid());

    private static JourneyStop Stop(OperationalJourney journey, Guid station) => journey.Stops.Single(s => s.StationId == station);

    // The train reached S2 `late` minutes late (a crew tap), so the journey delay is `late`.
    private static OperationalJourney LateAtS2(int late)
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 40).AddMinutes(late));
        journey.ClearDomainEvents();
        return journey;
    }

    [Fact]
    public void Skipping_a_stop_reduces_later_delay_by_its_scheduled_dwell()
    {
        var journey = LateAtS2(late: 9);

        var result = journey.SkipStopByController(0, S3, Ruwan, At(6, 50));   // S3 dwell is 5 minutes

        Assert.Equal(DecisionResult.Done, result);
        Assert.Equal(StopStatus.Skipped, Stop(journey, S3).Status);
        Assert.Equal(4, journey.DelayMinutes);                                   // 9 - 5
        Assert.Contains(new StopSkipped(journey.Id, S3, 3, At(6, 50)), journey.DomainEvents);
        Assert.Contains(journey.DomainEvents, e => e is DelayChanged { DelayMinutes: 4, PreviousDelayMinutes: 9 });
        Assert.Equal(1, journey.ControlVersion);
    }

    [Fact]
    public void Skipping_a_stop_never_makes_an_on_time_train_early()
    {
        var late = LateAtS2(late: 3);
        late.SkipStopByController(0, S3, Ruwan, At(6, 50));   // 3 - 5 would be -2
        Assert.Equal(0, late.DelayMinutes);

        var onTime = LateAtS2(late: 0);
        onTime.SkipStopByController(0, S3, Ruwan, At(6, 50));
        Assert.Equal(0, onTime.DelayMinutes);

        var early = NewJourney();
        early.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 38));   // 2 minutes early
        early.SkipStopByController(0, S3, Ruwan, At(6, 50));
        Assert.Equal(-2, early.DelayMinutes);   // an early train is unchanged
    }

    [Fact]
    public void Skipping_does_not_change_a_delay_the_controller_set()
    {
        var journey = NewJourney();
        journey.SetDelayByController(0, new Delay(12), new JourneyReason("Weather", null), Ruwan, At(6, 0));

        journey.SkipStopByController(1, S3, Ruwan, At(6, 50));

        Assert.Equal(new SourcedDelay(new Delay(12), Source.Controller), journey.CurrentDelay);   // R-05
        Assert.Equal(2, journey.ControlVersion);
    }

    [Fact]
    public void Skip_refused_for_origin_station()
    {
        var journey = NewJourney();

        Assert.Equal(DecisionResult.InvalidState, journey.SkipStopByController(0, S1, Ruwan, At(5, 0)));
        Assert.Equal(0, journey.ControlVersion);
    }

    [Fact]
    public void Skip_refused_for_a_stop_already_left_or_passed_or_reached()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S3, StopEventKind.Arrival, At(7, 0));   // S2 inferred Departed, S3 Arrived

        Assert.Equal(DecisionResult.InvalidState, journey.SkipStopByController(0, S2, Ruwan, At(7, 1)));
        Assert.Equal(DecisionResult.InvalidState, journey.SkipStopByController(0, S3, Ruwan, At(7, 1)));
        Assert.Equal(DecisionResult.Done, journey.SkipStopByController(0, S4, Ruwan, At(7, 1)));
    }

    [Fact]
    public void Skip_refused_for_the_terminus_and_for_a_stop_the_train_only_passes()
    {
        Assert.Equal(DecisionResult.InvalidState, NewJourney().SkipStopByController(0, S5, Ruwan, At(5, 0)));      // use terminate instead
        Assert.Equal(DecisionResult.InvalidState, NewJourney(withPass: true).SkipStopByController(0, S2, Ruwan, At(5, 0)));   // nothing to skip
    }

    [Fact]
    public void Skip_for_an_unknown_station_stale_version_or_closed_journey_is_refused()
    {
        var journey = NewJourney();
        Assert.Equal(DecisionResult.UnknownStop, journey.SkipStopByController(0, Guid.NewGuid(), Ruwan, At(5, 0)));
        Assert.Equal(DecisionResult.StaleVersion, journey.SkipStopByController(3, S3, Ruwan, At(5, 0)));

        journey.CancelByController(0, new JourneyReason("Other", null), Ruwan, At(5, 0));
        Assert.Equal(DecisionResult.InvalidState, journey.SkipStopByController(1, S3, Ruwan, At(5, 1)));
    }

    [Fact]
    public void A_skipped_stop_is_not_detected_by_gps_and_ignores_a_crew_tap()
    {
        var journey = NewJourney();
        journey.Start(At(5, 55), Guid.NewGuid(), Guid.NewGuid());
        journey.SkipStopByController(0, S3, Ruwan, At(6, 0));
        journey.ClearDomainEvents();

        // The train stands in S3's zone anyway.
        var at = At(7, 0);
        foreach (var (seconds, speed, metres) in new[] { (0, 40.0, 400.0), (10, 3.0, 100.0), (30, 0.0, 50.0) })
        {
            var time = at.AddSeconds(seconds);
            journey.RecordPosition(new PositionSample(new GeoPoint(7.0, 80.0), speed, time, time, TimeSpan.Zero, 0, new Dictionary<Guid, double> { [S3] = metres }), time);
        }

        journey.RecordCrewTime(S3, StopEventKind.Arrival, At(7, 0));

        Assert.Equal(StopStatus.Skipped, Stop(journey, S3).Status);
        Assert.Null(Stop(journey, S3).ActualArrival);
        Assert.DoesNotContain(journey.DomainEvents, e => e is StopStatusChanged { StationId: var id } && id == S3);
        Assert.Equal(0, journey.DelayMinutes);   // and it set no delay
    }

    [Fact]
    public void A_skipped_stop_is_not_the_next_stop_for_the_overdue_delay()
    {
        var journey = NewJourney();
        journey.Start(At(5, 55), Guid.NewGuid(), Guid.NewGuid());
        journey.RecordPosition(new PositionSample(new GeoPoint(7.0, 80.0), 40, At(6, 0), At(6, 0), TimeSpan.Zero, 0, new Dictionary<Guid, double>()), At(6, 0));
        journey.RecordCrewTime(S2, StopEventKind.Departure, At(6, 42));
        journey.SkipStopByController(0, S3, Ruwan, At(6, 43));

        journey.RefreshTracking(At(7, 20));   // S3 was due at 07:00; S4 is due at 07:30

        Assert.Equal(0, journey.DelayMinutes);
    }
}
