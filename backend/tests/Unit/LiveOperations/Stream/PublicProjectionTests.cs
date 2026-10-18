using System.Text.Json;
using System.Text.Json.Nodes;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Outbox;
using SmartRail.LiveOperations.Stream;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations.Stream;

// Threat T-07: the public stream never shows staff data. The projection keeps only what is on an allow-list.
public class PublicProjectionTests
{
    private static readonly Guid S1 = Guid.NewGuid();
    private static readonly Guid S2 = Guid.NewGuid();

    private static DateTime At(int hour, int minute) => new(2026, 10, 14, hour, minute, 0, DateTimeKind.Utc);

    private static EventEnvelope Envelope(string type, JsonObject payload) =>
        new(Guid.NewGuid(), type, 1, At(6, 44), Guid.NewGuid(), new DateOnly(2026, 10, 14), [S2], Guid.NewGuid(), Source.Crew, payload);

    private static EventEnvelope RealDelayChanged()
    {
        var journey = OperationalJourney.Plan(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 14), Guid.NewGuid(),
            [new PlannedStop(1, S1, CallType.Stop, null, 360, "1"), new PlannedStop(2, S2, CallType.Stop, 400, null, "2")],
            TimeZoneInfo.Utc, Guid.NewGuid());
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 44));
        return EventMapper.Map(journey.DomainEvents.OfType<DelayChanged>().Single(), journey, Guid.NewGuid());
    }

    [Fact]
    public void The_public_event_carries_the_sequence_and_the_envelope_fields_but_no_source()
    {
        var envelope = RealDelayChanged();

        var projected = PublicProjection.Of(envelope, sequence: 42)!;

        Assert.Equal(42, projected.Sequence);
        Assert.Equal(envelope.EventId, projected.EventId);
        Assert.Equal("DelayChanged", projected.Type);
        Assert.Equal(envelope.JourneyId, projected.JourneyId);
        Assert.Equal(envelope.LineId, projected.LineId);
        Assert.Equal(envelope.StationIds, projected.StationIds);
        var json = JsonSerializer.Serialize(projected, EventJson.Options);
        Assert.DoesNotContain("\"source\"", json);
        Assert.DoesNotContain("Crew", json);
    }

    [Fact]
    public void Payload_fields_outside_the_allow_list_are_dropped()
    {
        var payload = new JsonObject
        {
            ["stationId"] = S2.ToString(), ["sequence"] = 2, ["from"] = "Scheduled", ["to"] = "Arrived", ["at"] = "2026-10-14T06:44:00Z",
            ["crewSessionId"] = Guid.NewGuid().ToString(),
            ["actorId"] = Guid.NewGuid().ToString(),
            ["incidentId"] = Guid.NewGuid().ToString(),
        };

        var projected = PublicProjection.Of(Envelope("StopStatusChanged", payload), 7)!;

        Assert.Equal(["at", "from", "sequence", "stationId", "to"], projected.Payload.Select(p => p.Key).Order());
    }

    [Theory]
    [InlineData("IncidentReported")]
    [InlineData("UnscheduledStopDetected")]
    [InlineData("ProviderHealthChanged")]
    [InlineData("CrewMessageAcknowledged")]
    [InlineData("SomethingNobodyListedYet")]
    public void Event_types_not_on_the_public_list_are_never_projected(string type)
    {
        Assert.Null(PublicProjection.Of(Envelope(type, new JsonObject { ["note"] = "private" }), 1));
    }

    // T-07: which tablet and which cab session started a journey is staff information.
    [Fact]
    public void A_started_journey_is_public_but_its_device_and_cab_session_are_not()
    {
        var payload = new JsonObject { ["deviceId"] = Guid.NewGuid().ToString(), ["crewSessionId"] = Guid.NewGuid().ToString() };

        var projected = PublicProjection.Of(Envelope("JourneyStarted", payload), 9)!;

        Assert.Equal("JourneyStarted", projected.Type);
        Assert.Empty(projected.Payload);
        var json = JsonSerializer.Serialize(projected, EventJson.Options);
        Assert.DoesNotContain("deviceId", json);
        Assert.DoesNotContain("crewSessionId", json);
    }

    [Fact]
    public void An_ended_journey_keeps_its_reason()
    {
        var projected = PublicProjection.Of(Envelope("JourneyEnded", new JsonObject { ["reason"] = "Driver", ["note"] = "private" }), 10)!;

        Assert.Equal(["reason"], projected.Payload.Select(p => p.Key));
    }

    [Fact]
    public void Delay_change_keeps_the_remaining_stops_with_their_expected_times()
    {
        var projected = PublicProjection.Of(RealDelayChanged(), 3)!;

        Assert.Equal(4, projected.Payload["delayMinutes"]!.GetValue<int>());
        var stops = projected.Payload["stops"]!.AsArray();
        Assert.Single(stops);
        Assert.Equal(S2.ToString(), stops[0]!["stationId"]!.GetValue<string>());
    }
}
