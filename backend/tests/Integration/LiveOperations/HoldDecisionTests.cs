using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmartRail.IntegrationTests.Identity;
using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// POST /ops/journeys/{id}/hold and /hold/release (contract 3.5, US-O4, R-22). Service 1004 starts at Kandy (KDT) and calls at Peradeniya (PDA).
[Collection("Api")]
public class HoldDecisionTests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Day = new(2027, 3, 4);

    private readonly HttpClient _client = api.Factory.NewClient();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    private async Task<Guid> Station(string code) => (await RealJourneys.StationsAsync(api.Factory.Services, code))[code];

    private async Task<HttpResponseMessage> Post(string path, object body, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Authorization", "Bearer " + (token ?? await api.TokenForAsync("Controller", Ct)));
        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> Hold(Guid id, int version, Guid station, int minutes = 10, string? reason = "Congestion") =>
        await Post($"/api/v1/ops/journeys/{id}/hold", new { stationId = station, minutes, reasonCode = reason, note = reason is null ? null : "Waiting for the platform", expectedVersion = version });

    private static async Task<JsonElement> Body(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    [Fact]
    public async Task The_controller_holds_the_train_at_a_station_and_the_journey_shows_the_hold()
    {
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Day);
        var kandy = await Station("KDT");

        var response = await Hold(id, 0, kandy, minutes: 15);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Body(response);
        Assert.Equal(1, body.GetProperty("version").GetInt32());
        Assert.Equal(kandy, body.GetProperty("hold").GetProperty("stationId").GetGuid());
        Assert.Equal(15, body.GetProperty("hold").GetProperty("minutes").GetInt32());
        Assert.Equal(body.GetRawText(), (await _client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{id}", Ct)).GetRawText());   // what passengers see
    }

    [Fact]
    public async Task The_hold_is_announced_on_the_stream_with_its_reason_and_releasing_it_is_announced_too()
    {
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Day);
        var kandy = await Station("KDT");
        using var stream = await SseClient.ConnectAsync(api.Factory.CreateClient(), $"/api/v1/stream?journeyIds={id}", null, Ct);
        await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct);   // the snapshot

        await Hold(id, 0, kandy, 10);
        var held = await NextOf(stream, "JourneyHeld");
        Assert.False(held.GetProperty("payload").GetProperty("released").GetBoolean());
        Assert.Equal("Congestion", held.GetProperty("payload").GetProperty("reason").GetProperty("code").GetString());
        Assert.Equal(10, held.GetProperty("payload").GetProperty("minutes").GetInt32());

        var release = await Post($"/api/v1/ops/journeys/{id}/hold/release", new { expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, release.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await Body(release)).GetProperty("hold").ValueKind);
        var released = await NextOf(stream, "JourneyHeld");
        Assert.True(released.GetProperty("payload").GetProperty("released").GetBoolean());
        Assert.DoesNotContain("source", released.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<JsonElement> NextOf(SseClient stream, string type)
    {
        SseFrame? frame;
        do
        {
            frame = await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct);
            Assert.NotNull(frame);
        }
        while (frame!.Event != type);
        return frame.Json;
    }

    [Fact]
    public async Task The_hold_response_and_the_stream_snapshot_carry_the_reason_as_code_and_note_and_the_snapshot_holds_the_same_journey_detail()
    {
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Day);
        var kandy = await Station("KDT");
        var held = await Hold(id, 0, kandy, 10);
        var reason = (await Body(held)).GetProperty("hold").GetProperty("reason");
        Assert.Equal("Congestion", reason.GetProperty("code").GetString());
        Assert.Equal("Waiting for the platform", reason.GetProperty("note").GetString());

        using var stream = await SseClient.ConnectAsync(api.Factory.CreateClient(), $"/api/v1/stream?journeyIds={id}", null, Ct);
        var snapshot = (await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct))!.Json;

        // The snapshot's `journey` is, byte for byte, what GET /journeys/{id} answers: hold, reason, platforms, version, freshness.
        Assert.Equal((await _client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{id}", Ct)).GetRawText(), snapshot.GetProperty("journey").GetRawText());
        Assert.Equal(1, snapshot.GetProperty("journey").GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task A_hold_needs_a_station_and_minutes_and_a_known_reason()
    {
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Day);

        var response = await Post($"/api/v1/ops/journeys/{id}/hold", new { minutes = 0, reasonCode = "Aliens", expectedVersion = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await Body(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("stationId", out _));
        Assert.True(errors.TryGetProperty("minutes", out _));
        Assert.True(errors.TryGetProperty("reasonCode", out _));
    }

    [Fact]
    public async Task A_hold_without_a_reason_is_fine()
    {
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Day);

        Assert.Equal(HttpStatusCode.OK, (await Hold(id, 0, await Station("KDT"), 5, reason: null)).StatusCode);
    }

    [Fact]
    public async Task A_hold_at_a_station_that_is_not_on_the_journey_is_a_400()
    {
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Day);

        var response = await Hold(id, 0, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await Body(response)).GetProperty("errors").TryGetProperty("stationId", out _));
    }

    [Fact]
    public async Task A_hold_at_a_station_the_train_has_left_or_a_second_hold_is_409_invalid_state()
    {
        var kandy = await Station("KDT");
        var left = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Day, j =>
            j.RecordCrewTime(kandy, StopEventKind.Departure, j.Stops[0].ScheduledDeparture!.Value));
        var afterDeparture = await Hold(left, 0, kandy);
        Assert.Equal(HttpStatusCode.Conflict, afterDeparture.StatusCode);
        Assert.Equal("INVALID_STATE", (await Body(afterDeparture)).GetProperty("code").GetString());

        var held = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Day.AddDays(1));   // one journey per service and date
        await Hold(held, 0, kandy);
        var second = await Hold(held, 1, kandy);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("INVALID_STATE", (await Body(second)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Releasing_when_nothing_is_held_is_409_and_a_stale_hold_is_409_version_conflict()
    {
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Day);
        var kandy = await Station("KDT");

        var nothing = await Post($"/api/v1/ops/journeys/{id}/hold/release", new { expectedVersion = 0 });
        Assert.Equal("INVALID_STATE", (await Body(nothing)).GetProperty("code").GetString());

        await Hold(id, 0, kandy);
        var stale = await Hold(id, 0, kandy);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("VERSION_CONFLICT", (await Body(stale)).GetProperty("code").GetString());
    }
}
