using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// A stop that gets an actual time means the train is past every earlier stop (inferred Departed, or Passed for a Pass
// call type; no time), and the
// journey delay follows the most recent stop event (R-01, R-05 clarified 15 Oct).
public class StopSequenceTests
{
    private static readonly Guid S1 = Guid.NewGuid();   // origin, departs 06:00
    private static readonly Guid S2 = Guid.NewGuid();   // 06:40 / 06:42
    private static readonly Guid S3 = Guid.NewGuid();   // 07:00 / 07:02
    private static readonly Guid S4 = Guid.NewGuid();   // terminus, arrives 07:30

    private static DateTime At(int hour, int minute, int second = 0) =>
        new(2026, 10, 14, hour, minute, second, DateTimeKind.Utc);

    private static OperationalJourney NewJourney(CallType s3 = CallType.Stop) =>
        OperationalJourney.Plan(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 14), Guid.NewGuid(),
            [
                new PlannedStop(1, S1, CallType.Stop, null, 360, "1"),
                new PlannedStop(2, S2, CallType.Stop, 400, 402, "2"),
                new PlannedStop(3, S3, s3, 420, 422, "3"),
                new PlannedStop(4, S4, CallType.Stop, 450, null, "4"),
            ],
            TimeZoneInfo.Utc, Guid.NewGuid());

    private static JourneyStop Stop(OperationalJourney journey, Guid station) =>
        journey.Stops.Single(s => s.StationId == station);

    private static PositionSample Near(DateTime time, double speedKmh, Guid station, double metres) =>
        new(new GeoPoint(7.0, 80.0), speedKmh, time, time, TimeSpan.Zero, 0, new Dictionary<Guid, double> { [station] = metres });

    // GPS sees the train stop at `station` from `stoppedAt` (two samples 20 s apart).
    private static void GpsStopsAt(OperationalJourney journey, Guid station, DateTime stoppedAt)
    {
        journey.RecordPosition(Near(stoppedAt, 3, station, 100), stoppedAt);
        journey.RecordPosition(Near(stoppedAt.AddSeconds(20), 0, station, 50), stoppedAt.AddSeconds(20));
    }

    // ---- Inferred passes -------------------------------------------------------------------------------------

    [Fact]
    public void Skipped_detection_at_stop_3_then_arrival_at_stop_4_marks_stop_3_departed_and_takes_the_delay_from_stop_4()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 40));
        journey.RecordCrewTime(S2, StopEventKind.Departure, At(6, 42));
        // The train goes through stop 3 and GPS misses it. Then it stops at stop 4, 4 min late.

        GpsStopsAt(journey, S4, At(7, 34));

        var skipped = Stop(journey, S3);
        Assert.Equal(StopStatus.Departed, skipped.Status);   // it is a Stop: "Passed" would say the train did not stop there
        Assert.Null(skipped.ActualArrivalTime);               // inferred: no time is invented
        Assert.Null(skipped.ActualDepartureTime);
        Assert.Equal(new SourcedDelay(new Delay(4), Source.Gps), journey.CurrentDelay);
        Assert.Contains(new StopStatusChanged(journey.Id, S3, 3, StopStatus.Scheduled, StopStatus.Departed, At(7, 34), Source.Gps), journey.DomainEvents);
    }

    [Fact]
    public void Overdue_delay_is_not_measured_against_a_stop_the_train_has_passed()
    {
        var journey = NewJourney();
        journey.RecordPosition(Near(At(6, 30), 60, S2, 5000), At(6, 30));   // tracked
        journey.RecordCrewTime(S2, StopEventKind.Departure, At(6, 42));
        GpsStopsAt(journey, S4, At(7, 34));                                   // stop 3 missed by GPS

        journey.RefreshTracking(At(7, 50));   // stop 3 was due 07:00: it would show 50 min late if it still counted

        Assert.Equal(4, journey.DelayMinutes);
    }

    [Fact]
    public void Every_earlier_stop_still_scheduled_is_inferred_in_order()
    {
        var journey = NewJourney();

        journey.RecordCrewTime(S3, StopEventKind.Arrival, At(7, 3));

        Assert.Equal(StopStatus.Departed, Stop(journey, S1).Status);
        Assert.Equal(StopStatus.Departed, Stop(journey, S2).Status);
        Assert.Equal(StopStatus.Arrived, Stop(journey, S3).Status);
        Assert.Equal(StopStatus.Scheduled, Stop(journey, S4).Status);
        Assert.Equal(
            [S1, S2, S3],
            journey.DomainEvents.OfType<StopStatusChanged>().Select(e => e.StationId));
    }

    [Fact]
    public void A_crew_departure_tap_also_infers_earlier_stops()
    {
        var journey = NewJourney();

        journey.RecordCrewTime(S3, StopEventKind.Departure, At(7, 5));

        Assert.Equal(StopStatus.Departed, Stop(journey, S2).Status);
    }

    [Fact]
    public void Earlier_stops_that_arrived_or_departed_are_left_alone()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S1, StopEventKind.Departure, At(6, 0));
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 40));

        journey.RecordCrewTime(S3, StopEventKind.Arrival, At(7, 0));

        Assert.Equal(StopStatus.Departed, Stop(journey, S1).Status);
        Assert.Equal(StopStatus.Arrived, Stop(journey, S2).Status);   // departure tap was missed; not guessed
    }

    [Fact]
    public void An_inferred_stop_is_not_detected_again_by_later_positions()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S3, StopEventKind.Arrival, At(7, 3));
        var delayBefore = journey.CurrentDelay;

        GpsStopsAt(journey, S2, At(7, 5));   // GPS noise near stop 2 afterwards

        Assert.Equal(StopStatus.Departed, Stop(journey, S2).Status);
        Assert.Null(Stop(journey, S2).ActualArrivalTime);
        Assert.Equal(delayBefore, journey.CurrentDelay);
    }

    [Fact]
    public void Inferred_stop_with_call_type_stop_becomes_departed_without_a_time()
    {
        var journey = NewJourney(s3: CallType.Stop);

        journey.RecordCrewTime(S4, StopEventKind.Arrival, At(7, 31));

        var inferred = Stop(journey, S3);
        Assert.Equal(StopStatus.Departed, inferred.Status);
        Assert.Null(inferred.ActualArrivalTime);
        Assert.Null(inferred.ActualDepartureTime);
    }

    [Fact]
    public void Inferred_stop_with_call_type_pass_becomes_passed_without_a_time()
    {
        var journey = NewJourney(s3: CallType.Pass);

        journey.RecordCrewTime(S4, StopEventKind.Arrival, At(7, 31));

        var inferred = Stop(journey, S3);
        Assert.Equal(StopStatus.Passed, inferred.Status);
        Assert.Null(inferred.ActualArrivalTime);
        Assert.Null(inferred.ActualDepartureTime);
        Assert.Contains(new StopStatusChanged(journey.Id, S3, 3, StopStatus.Scheduled, StopStatus.Passed, At(7, 31), Source.Crew), journey.DomainEvents);
    }

    [Fact]
    public void Origin_becomes_departed_when_a_later_stop_is_reached()
    {
        var journey = NewJourney();

        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 40));

        Assert.Equal(StopStatus.Departed, Stop(journey, S1).Status);
        Assert.Null(Stop(journey, S1).ActualDepartureTime);
        Assert.Contains(new StopStatusChanged(journey.Id, S1, 1, StopStatus.Scheduled, StopStatus.Departed, At(6, 40), Source.Crew), journey.DomainEvents);
    }

    // An inferred stop has no time, so it cannot move the delay: only the stop that was reached does.
    [Fact]
    public void Inferred_stops_do_not_change_the_delay()
    {
        var journey = NewJourney();

        journey.RecordCrewTime(S3, StopEventKind.Arrival, At(7, 3));   // 3 min late

        Assert.Equal(new SourcedDelay(new Delay(3), Source.Crew), journey.CurrentDelay);
        Assert.Single(journey.DomainEvents.OfType<DelayChanged>());
    }

    // R-20 will settle out-of-order taps. Until then a late tap for an inferred stop records its time and keeps the status.
    [Fact]
    public void A_late_tap_for_an_inferred_stop_records_its_time_and_keeps_the_status()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S3, StopEventKind.Arrival, At(7, 3));   // infers stops 1 and 2

        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 41));

        Assert.Equal(new TimeWithSource(At(6, 41), Source.Crew), Stop(journey, S2).ActualArrivalTime);
        Assert.Equal(StopStatus.Departed, Stop(journey, S2).Status);
    }

    // ---- The delay follows the most recent stop event (R-05 clarified) ------------------------------------------

    [Fact]
    public void Crew_arrival_then_gps_arrival_at_next_stop_makes_the_delay_follow_the_next_stop()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 43));   // 3 min late, Crew
        Assert.Equal(new SourcedDelay(new Delay(3), Source.Crew), journey.CurrentDelay);

        GpsStopsAt(journey, S3, At(7, 6));                               // 6 min late, GPS

        Assert.Equal(new SourcedDelay(new Delay(6), Source.Gps), journey.CurrentDelay);
    }

    [Fact]
    public void Older_stop_event_does_not_move_the_delay_back()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 43));
        journey.RecordCrewTime(S3, StopEventKind.Arrival, At(7, 5));    // 5 min late: the latest event
        Assert.Equal(5, journey.DelayMinutes);

        journey.RecordCrewTime(S2, StopEventKind.Departure, At(6, 46)); // a tap delivered late, 4 min late at stop 2

        Assert.Equal(StopStatus.Departed, Stop(journey, S2).Status);
        Assert.Equal(At(6, 46), Stop(journey, S2).ActualDeparture);
        Assert.Equal(5, journey.DelayMinutes);
    }

    [Fact]
    public void A_real_stop_time_replaces_the_overdue_delay_whatever_its_source()
    {
        var journey = NewJourney();
        journey.RecordPosition(Near(At(6, 30), 60, S2, 5000), At(6, 30));   // tracked
        journey.RefreshTracking(At(6, 50));                                  // stop 2 overdue: 10 min late (Schedule)
        Assert.Equal(new SourcedDelay(new Delay(10), Source.Schedule), journey.CurrentDelay);

        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 44));

        Assert.Equal(new SourcedDelay(new Delay(4), Source.Crew), journey.CurrentDelay);
    }

    [Fact]
    public void Controller_delay_still_blocks_stop_events_but_inference_continues()
    {
        var journey = NewJourney();
        journey.SetControllerDelay(new Delay(10), At(6, 30));

        journey.RecordCrewTime(S3, StopEventKind.Arrival, At(7, 3));

        Assert.Equal(new SourcedDelay(new Delay(10), Source.Controller), journey.CurrentDelay);
        Assert.Equal(StopStatus.Departed, Stop(journey, S2).Status);
    }
}
