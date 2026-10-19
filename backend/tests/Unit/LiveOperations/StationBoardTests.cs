using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.StationBoard;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations;

// US-D1: what is on a station board. S2 is the station of the board.
public class StationBoardTests
{
    private static readonly Guid S1 = Guid.NewGuid();   // origin, departs 06:00
    private static readonly Guid S2 = Guid.NewGuid();   // arrives 06:40, departs 06:42  (the board's station)
    private static readonly Guid S3 = Guid.NewGuid();   // terminus, arrives 07:30
    private static readonly Guid Ruwan = Guid.NewGuid();

    private static DateTime At(int hour, int minute, int second = 0) => new(2026, 10, 14, hour, minute, second, DateTimeKind.Utc);

    private static OperationalJourney NewJourney(CallType callAtS2 = CallType.Stop) =>
        OperationalJourney.Plan(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 14), Guid.NewGuid(),
            [
                new PlannedStop(1, S1, CallType.Stop, null, 360, "1"),
                new PlannedStop(2, S2, callAtS2, 400, 402, "2"),
                new PlannedStop(3, S3, CallType.Stop, 450, null, "3"),
            ],
            TimeZoneInfo.Utc, Guid.NewGuid());

    private static OperationalJourney StartedJourney()
    {
        var journey = NewJourney();
        journey.Start(At(5, 55), Guid.NewGuid(), Guid.NewGuid());
        return journey;
    }

    private static List<StationBoardBuilder.Candidate> Board(BoardKind kind, DateTime now, int limit, params OperationalJourney[] journeys) =>
        StationBoardBuilder.Candidates(journeys, S2, kind, now, limit);

    [Fact]
    public void A_departures_board_has_no_terminus_and_an_arrivals_board_no_origin()
    {
        var journey = NewJourney();

        Assert.Single(StationBoardBuilder.Candidates([journey], S2, BoardKind.Departures, At(6, 0), 10));
        Assert.Empty(StationBoardBuilder.Candidates([journey], S3, BoardKind.Departures, At(6, 0), 10));
        Assert.Empty(StationBoardBuilder.Candidates([journey], S1, BoardKind.Arrivals, At(6, 0), 10));
        Assert.Single(StationBoardBuilder.Candidates([journey], S3, BoardKind.Arrivals, At(6, 0), 10));
    }

    [Fact]
    public void A_train_that_has_left_disappears_one_minute_after_it_left()
    {
        var journey = StartedJourney();
        journey.RecordCrewTime(S2, StopEventKind.Departure, At(6, 43));

        Assert.Single(Board(BoardKind.Departures, At(6, 43, 59), 10, journey));    // 59 seconds after: still there
        Assert.Empty(Board(BoardKind.Departures, At(6, 44, 1), 10, journey));      // more than a minute: gone
    }

    [Fact]
    public void An_arrivals_board_drops_a_train_a_minute_after_it_arrived()
    {
        var journey = StartedJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 41));

        Assert.Single(Board(BoardKind.Arrivals, At(6, 41, 30), 10, journey));
        Assert.Empty(Board(BoardKind.Arrivals, At(6, 42, 30), 10, journey));
        Assert.Single(Board(BoardKind.Departures, At(6, 42, 30), 10, journey));   // but it is still to depart
    }

    [Fact]
    public void A_late_train_is_ordered_by_its_expected_time_not_its_scheduled_time()
    {
        var late = NewJourney();
        late.SetDelayByController(0, new Delay(15), new JourneyReason("Weather", null), Ruwan, At(6, 0));   // scheduled 06:42, expected 06:57
        var onTime = NewJourney();                                                                         // scheduled 06:42, expected 06:42

        var rows = Board(BoardKind.Departures, At(6, 41), 10, late, onTime);

        Assert.Equal([onTime.Id, late.Id], rows.Select(r => r.Journey.Id));
        Assert.Equal(At(6, 57), rows[1].ExpectedTime);
        Assert.Equal(At(6, 42), rows[1].Time);
    }

    [Fact]
    public void A_train_nobody_has_started_is_not_shown_once_its_time_has_passed()
    {
        var planned = NewJourney();

        Assert.Single(Board(BoardKind.Departures, At(6, 42, 30), 10, planned));
        Assert.Empty(Board(BoardKind.Departures, At(6, 44), 10, planned));
    }

    [Fact]
    public void A_cancelled_train_stays_until_its_scheduled_time_has_passed()
    {
        var journey = NewJourney();
        journey.CancelByController(0, new JourneyReason("TrainFault", null), Ruwan, At(6, 0));

        Assert.Single(Board(BoardKind.Departures, At(6, 41), 10, journey));
        Assert.Empty(Board(BoardKind.Departures, At(6, 43), 10, journey));
    }

    [Fact]
    public void A_train_that_only_passes_is_on_the_board_with_call_type_Pass()
    {
        var journey = NewJourney(CallType.Pass);

        var row = Assert.Single(Board(BoardKind.Departures, At(6, 0), 10, journey));
        Assert.Equal(CallType.Pass, row.Stop.CallType);
    }

    [Fact]
    public void The_limit_keeps_the_next_trains_only()
    {
        var rows = Board(BoardKind.Departures, At(5, 0), 2, NewJourney(), NewJourney(), NewJourney());

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void The_destination_after_an_early_termination_is_the_termination_station()
    {
        var journey = StartedJourney();
        journey.RecordCrewTime(S1, StopEventKind.Departure, At(6, 0));
        journey.TerminateByController(0, S2, new JourneyReason("TrainFault", null), Ruwan, At(6, 20));

        var (origin, destination) = StationBoardBuilder.Ends(journey);

        Assert.Equal((S1, S2), (origin, destination));
        Assert.Equal((S1, S3), StationBoardBuilder.Ends(NewJourney()));
    }
}
