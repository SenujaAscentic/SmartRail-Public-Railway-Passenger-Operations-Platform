using System.Text.Json;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Outbox;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations.Outbox;

// Domain events -> the envelope of domain model 8.1 and the payloads of 8.2.
public class EventMapperTests
{
    private static readonly Guid S1 = Guid.NewGuid();   // origin, departs 06:00
    private static readonly Guid S2 = Guid.NewGuid();   // 06:40 / 06:42
    private static readonly Guid S3 = Guid.NewGuid();   // terminus, arrives 07:30
    private static readonly Guid Line = Guid.NewGuid();

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
            TimeZoneInfo.Utc, Line);

    private static EventEnvelope MapSingle<T>(OperationalJourney journey) where T : IDomainEvent =>
        EventMapper.Map(journey.DomainEvents.OfType<T>().Single(), journey, Guid.NewGuid());

    [Fact]
    public void Envelope_carries_the_journey_line_date_type_version_and_the_event_time()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 44));
        var id = Guid.NewGuid();

        var envelope = EventMapper.Map(journey.DomainEvents.OfType<DelayChanged>().Single(), journey, id);

        Assert.Equal(id, envelope.EventId);
        Assert.Equal("DelayChanged", envelope.Type);
        Assert.Equal(1, envelope.Version);
        Assert.Equal(At(6, 44), envelope.OccurredAt);
        Assert.Equal(journey.Id, envelope.JourneyId);
        Assert.Equal(new DateOnly(2026, 10, 14), envelope.ServiceDate);
        Assert.Equal(Line, envelope.LineId);
        Assert.Equal(Source.Crew, envelope.Source);
    }

    [Fact]
    public void Stop_status_change_names_its_own_station_and_the_status_change()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 44));
        var arrived = journey.DomainEvents.OfType<StopStatusChanged>().Single(e => e.StationId == S2);

        var envelope = EventMapper.Map(arrived, journey, Guid.NewGuid());

        Assert.Equal("StopStatusChanged", envelope.Type);
        Assert.Equal([S2], envelope.StationIds);
        var payload = envelope.Payload.Deserialize<StopStatusChangedPayload>(EventJson.Options)!;
        Assert.Equal(new StopStatusChangedPayload(S2, 2, StopStatus.Scheduled, StopStatus.Arrived, At(6, 44)), payload);
    }

    [Fact]
    public void Delay_change_lists_the_remaining_stops_with_their_expected_times()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 44));   // 4 min late; stop 1 is inferred Departed

        var envelope = MapSingle<DelayChanged>(journey);

        var payload = envelope.Payload.Deserialize<DelayChangedPayload>(EventJson.Options)!;
        Assert.Equal(4, payload.DelayMinutes);
        Assert.Equal(0, payload.PreviousDelayMinutes);
        Assert.Equal(
            [
                new RemainingStop(S2, At(6, 44), At(6, 46)),   // arrived at 06:44; leaves 06:42 + 4 min
                new RemainingStop(S3, At(7, 34), null),        // the terminus has no departure
            ],
            payload.Stops);
        Assert.Equal([S2, S3], envelope.StationIds);            // every remaining stop's display needs the new delay
    }

    [Fact]
    public void Freshness_change_names_the_last_station_the_train_was_known_at()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 44));
        journey.RecordPosition(
            new PositionSample(new GeoPoint(7.0, 80.0), 40, At(6, 50), At(6, 50), TimeSpan.Zero, 0, new Dictionary<Guid, double>()),
            At(6, 50));

        var envelope = MapSingle<DataFreshnessChanged>(journey);

        Assert.Equal("DataFreshnessChanged", envelope.Type);
        var payload = envelope.Payload.Deserialize<DataFreshnessChangedPayload>(EventJson.Options)!;
        Assert.Equal(new DataFreshnessChangedPayload(TrackingStatus.NotStarted, TrackingStatus.Live, At(6, 50), S2), payload);
        Assert.Equal([S2, S3], envelope.StationIds);
        Assert.Equal(Source.Gps, envelope.Source);   // the freshness comes from GPS data
    }

    [Fact]
    public void Freshness_change_has_no_last_station_before_the_train_has_reached_any()
    {
        var journey = NewJourney();
        journey.RecordPosition(
            new PositionSample(new GeoPoint(7.0, 80.0), 40, At(6, 1), At(6, 1), TimeSpan.Zero, 0, new Dictionary<Guid, double>()),
            At(6, 1));

        var payload = MapSingle<DataFreshnessChanged>(journey).Payload.Deserialize<DataFreshnessChangedPayload>(EventJson.Options)!;

        Assert.Null(payload.LastStationId);
    }

    [Fact]
    public void Journey_started_keeps_the_tablet_and_the_cab_session_for_the_staff_side_and_names_every_stop()
    {
        var journey = NewJourney();
        var device = Guid.NewGuid();
        var session = Guid.NewGuid();
        journey.Start(At(5, 40), device, session);

        var envelope = MapSingle<JourneyStarted>(journey);

        Assert.Equal("JourneyStarted", envelope.Type);
        Assert.Equal(At(5, 40), envelope.OccurredAt);
        Assert.Equal(Source.Crew, envelope.Source);
        Assert.Equal([S1, S2, S3], envelope.StationIds);
        Assert.Equal(new JourneyStartedPayload(device, session), envelope.Payload.Deserialize<JourneyStartedPayload>(EventJson.Options));
    }

    [Fact]
    public void Journey_ended_says_why_it_ended()
    {
        var journey = NewJourney();
        journey.Start(At(5, 40), Guid.NewGuid(), Guid.NewGuid());
        journey.End(At(7, 31));

        var envelope = MapSingle<JourneyEnded>(journey);

        Assert.Equal("JourneyEnded", envelope.Type);
        Assert.Equal(new JourneyEndedPayload(JourneyEndReason.Driver), envelope.Payload.Deserialize<JourneyEndedPayload>(EventJson.Options));
        Assert.Equal([S1, S2, S3], envelope.StationIds);
    }

    [Fact]
    public void Json_uses_camel_case_names_and_enum_names_and_omits_empty_fields()
    {
        var journey = NewJourney();
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 44));

        var json = JsonSerializer.Serialize(MapSingle<DelayChanged>(journey), EventJson.Options);

        Assert.Contains("\"type\":\"DelayChanged\"", json);
        Assert.Contains("\"source\":\"Crew\"", json);
        Assert.Contains("\"delayMinutes\":4", json);
        Assert.Contains("\"serviceDate\":\"2026-10-14\"", json);
        Assert.DoesNotContain("expectedDeparture\":null", json);   // the terminus has none: the field is left out
    }
}
