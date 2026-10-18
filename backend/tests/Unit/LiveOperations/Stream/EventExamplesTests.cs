using System.Globalization;
using System.Text.Json;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Features.StreamEvents;
using SmartRail.LiveOperations.Outbox;
using SmartRail.LiveOperations.Stream;
using Xunit;

namespace SmartRail.UnitTests.LiveOperations.Stream;

// CLAUDE.md section 5, step 5: every stream event type has one example JSON in shared/events/, and the tests of every
// side parse those files. Here: the backend parses them, and what the backend really sends has the same shape.
public class EventExamplesTests
{
    private static readonly Guid S1 = Guid.NewGuid();
    private static readonly Guid S2 = Guid.NewGuid();
    private static readonly Guid S3 = Guid.NewGuid();

    private static string EventsFolder =>
        Path.GetFullPath(Path.Combine(global::SmartRail.Network.Seed.SeedFolder.Find(configuredPath: null), "..", "events"));

    private static JsonElement Example(string name) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(EventsFolder, name + ".json"))).RootElement;

    private static DateTime At(int hour, int minute, int second = 0) => new(2026, 10, 14, hour, minute, second, DateTimeKind.Utc);

    // ---- every type has an example, and it parses ----

    public static TheoryData<string> PublicTypes
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var type in PublicProjection.PublicEventTypes)
            {
                data.Add(type);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(PublicTypes))]
    public void Every_public_event_type_has_an_example_that_parses_as_a_public_event(string type)
    {
        var example = Example(type);

        var parsed = example.Deserialize<PublicEvent>(EventJson.Options)!;

        Assert.Equal(type, parsed.Type);
        Assert.Equal(1, parsed.Version);
        Assert.NotEqual(Guid.Empty, parsed.EventId);
        Assert.NotEqual(Guid.Empty, parsed.JourneyId);
        Assert.True(parsed.Sequence > 0);
        Assert.NotEmpty(parsed.StationIds);
    }

    [Fact]
    public void The_snapshot_example_parses_as_a_journey_snapshot()
    {
        var parsed = Example("snapshot").Deserialize<JourneySnapshot>(EventJson.Options)!;

        Assert.NotEqual(Guid.Empty, parsed.JourneyId);
        Assert.NotEmpty(parsed.Stops);
        Assert.True(parsed.Sequence > 0);
    }

    [Fact]
    public void The_resync_example_has_a_reason()
    {
        Assert.False(string.IsNullOrWhiteSpace(Example("resync").GetProperty("reason").GetString()));
    }

    // T-07: an example is a promise to client authors, so it must not show fields the public stream never sends.
    [Theory]
    [InlineData("StopStatusChanged")]
    [InlineData("DelayChanged")]
    [InlineData("DataFreshnessChanged")]
    [InlineData("JourneyStarted")]
    [InlineData("JourneyEnded")]
    [InlineData("TrainPositionUpdated")]
    [InlineData("snapshot")]
    public void Examples_show_no_staff_fields(string name)
    {
        var text = File.ReadAllText(Path.Combine(EventsFolder, name + ".json"));

        foreach (var forbidden in new[] { "source", "crewSessionId", "actorId", "causedBy", "incident", "deviceId" })
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---- what the backend really sends has the shape of the example ----

    private static OperationalJourney Journey()
    {
        var journey = OperationalJourney.Plan(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 14), Guid.NewGuid(),
            [
                new PlannedStop(1, S1, CallType.Stop, null, 360, "1"),
                new PlannedStop(2, S2, CallType.Stop, 400, 402, "2"),
                new PlannedStop(3, S3, CallType.Stop, 450, null, "3"),
            ],
            TimeZoneInfo.Utc, Guid.NewGuid());
        journey.RecordCrewTime(S1, StopEventKind.Departure, At(6, 1));
        journey.RecordCrewTime(S2, StopEventKind.Arrival, At(6, 44));
        journey.RecordPosition(
            new PositionSample(new GeoPoint(7.2906, 80.6337), 40, At(6, 50), At(6, 50), TimeSpan.Zero, 0, new Dictionary<Guid, double>()),
            At(6, 50));
        journey.AnnouncePosition(At(6, 50), new GeoPoint(7.2906, 80.6337), 62.5, S2, S3, 0.42);
        return journey;
    }

    // The JSON the stream puts in `data:` for this event: mapper, then the public projection, then the serialiser.
    private static JsonElement RealEvent<T>(OperationalJourney journey) where T : IDomainEvent
    {
        var envelope = EventMapper.Map(journey.DomainEvents.OfType<T>().Last(), journey, Guid.NewGuid());
        var publicEvent = PublicProjection.Of(envelope, sequence: 1)!;
        return JsonSerializer.SerializeToElement(publicEvent, EventJson.Options);
    }

    [Fact]
    public void StopStatusChanged_as_sent_has_the_shape_of_its_example()
    {
        Assert.Equal(Shape.Of(Example("StopStatusChanged")), Shape.Of(RealEvent<StopStatusChanged>(Journey())));
    }

    [Fact]
    public void DelayChanged_as_sent_has_the_shape_of_its_example()
    {
        Assert.Equal(Shape.Of(Example("DelayChanged")), Shape.Of(RealEvent<DelayChanged>(Journey())));
    }

    [Fact]
    public void DataFreshnessChanged_as_sent_has_the_shape_of_its_example()
    {
        Assert.Equal(Shape.Of(Example("DataFreshnessChanged")), Shape.Of(RealEvent<DataFreshnessChanged>(Journey())));
    }

    // A journey that was started and then ended, to produce the two lifecycle events.
    private static OperationalJourney Lifecycle()
    {
        var journey = OperationalJourney.Plan(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 14), Guid.NewGuid(),
            [new PlannedStop(1, S1, CallType.Stop, null, 360, "1"), new PlannedStop(2, S2, CallType.Stop, 400, null, "2")],
            TimeZoneInfo.Utc, Guid.NewGuid());
        journey.Start(At(5, 40), Guid.NewGuid(), Guid.NewGuid());
        journey.End(At(6, 58));
        return journey;
    }

    [Fact]
    public void TrainPositionUpdated_as_sent_has_the_shape_of_its_example()
    {
        Assert.Equal(Shape.Of(Example("TrainPositionUpdated")), Shape.Of(RealEvent<TrainPositionUpdated>(Journey())));
    }

    [Fact]
    public void JourneyStarted_as_sent_has_the_shape_of_its_example_and_an_empty_payload()
    {
        var real = RealEvent<JourneyStarted>(Lifecycle());

        Assert.Equal(Shape.Of(Example("JourneyStarted")), Shape.Of(real));
        Assert.Empty(real.GetProperty("payload").EnumerateObject());   // no device, no cab session (T-07)
    }

    [Fact]
    public void JourneyEnded_as_sent_has_the_shape_of_its_example()
    {
        Assert.Equal(Shape.Of(Example("JourneyEnded")), Shape.Of(RealEvent<JourneyEnded>(Lifecycle())));
    }

    [Fact]
    public void The_snapshot_as_sent_has_the_shape_of_its_example()
    {
        var real = JsonSerializer.SerializeToElement(SnapshotReader.ToSnapshot(Journey(), sequence: 1), EventJson.Options);

        Assert.Equal(Shape.Of(Example("snapshot")), Shape.Of(real));
    }

    [Fact]
    public void The_resync_as_sent_has_the_shape_of_its_example()
    {
        Assert.Equal(Shape.Of(Example("resync")), Shape.Of(JsonDocument.Parse(StreamHandler.ResyncData).RootElement));
    }

    // ---- the examples fit the records the API description is generated from ----

    private static readonly JsonSerializerOptions Strict = new(EventJson.Options) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };

    // An example with a field the record lacks (or the reverse) would mean the generated app types disagree with the wire.
    [Theory]
    [InlineData("StopStatusChanged", typeof(StopStatusChangedEvent))]
    [InlineData("DelayChanged", typeof(DelayChangedEvent))]
    [InlineData("DataFreshnessChanged", typeof(DataFreshnessChangedEvent))]
    [InlineData("JourneyStarted", typeof(JourneyStartedEvent))]
    [InlineData("JourneyEnded", typeof(JourneyEndedEvent))]
    [InlineData("TrainPositionUpdated", typeof(TrainPositionUpdatedEvent))]
    [InlineData("snapshot", typeof(JourneySnapshot))]
    [InlineData("resync", typeof(StreamResync))]
    public void Examples_fit_the_schema_records_exactly(string name, Type schemaType)
    {
        var example = Example(name);

        var parsed = example.Deserialize(schemaType, Strict);   // throws on a field the record does not have

        Assert.Equal(Shape.Of(example), Shape.Of(JsonSerializer.SerializeToElement(parsed, schemaType, EventJson.Options)));
    }

    [Theory]
    [InlineData("StopStatusChanged")]
    [InlineData("DelayChanged")]
    [InlineData("DataFreshnessChanged")]
    [InlineData("JourneyStarted")]
    [InlineData("JourneyEnded")]
    [InlineData("TrainPositionUpdated")]
    public void The_type_of_each_event_example_is_its_own_name(string name)
    {
        Assert.Equal(name, Example(name).GetProperty("type").GetString());
    }

    // The shape of a JSON value: every property path with its kind. Arrays are looked at through all their items together,
    // so an optional field that only some items have (an origin has no arrival) is still part of the shape.
    // Strings are told apart by what they hold (guid, date-time, date), because clients parse them differently.
    private static class Shape
    {
        public static List<string> Of(JsonElement element)
        {
            var shape = new SortedSet<string>(StringComparer.Ordinal);
            Walk(element, "$", shape);
            return [.. shape];
        }

        private static void Walk(JsonElement element, string path, SortedSet<string> shape)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    shape.Add($"{path}: object");
                    foreach (var property in element.EnumerateObject())
                    {
                        Walk(property.Value, $"{path}.{property.Name}", shape);
                    }
                    break;

                case JsonValueKind.Array:
                    shape.Add($"{path}: array");
                    foreach (var item in element.EnumerateArray())
                    {
                        Walk(item, $"{path}[]", shape);
                    }
                    break;

                case JsonValueKind.String:
                    shape.Add($"{path}: {StringKind(element.GetString()!)}");
                    break;

                default:
                    shape.Add($"{path}: {element.ValueKind.ToString().ToLowerInvariant()}");
                    break;
            }
        }

        private static string StringKind(string text) =>
            Guid.TryParse(text, out _) ? "guid"
            : DateTime.TryParseExact(text, "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out _) ? "date-time"
            : DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? "date"
            : "string";
    }
}
