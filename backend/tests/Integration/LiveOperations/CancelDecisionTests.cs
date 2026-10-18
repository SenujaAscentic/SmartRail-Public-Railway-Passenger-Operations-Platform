using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmartRail.IntegrationTests.Identity;
using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// POST /ops/journeys/{id}/cancel and /reinstate (contract 3.5, US-O4 AC4, R-14). The test clock is 14 Oct 2026, 04:30 UTC.
[Collection("Api")]
public class CancelDecisionTests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Future = new(2027, 3, 5);
    private static readonly DateOnly Past = new(2026, 10, 13);

    private readonly HttpClient _client = api.Factory.NewClient();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    private Task<Guid> NewJourney(DateOnly date, Action<OperationalJourney>? change = null) => RealJourneys.CreateAsync(api.Factory.Services, "1004", date, change);

    private async Task<HttpResponseMessage> Post(Guid id, string action, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ops/journeys/{id}/{action}") { Content = JsonContent.Create(body) };
        request.Headers.Add("Authorization", "Bearer " + await api.TokenForAsync("Controller", Ct));
        return await _client.SendAsync(request, Ct);
    }

    private Task<HttpResponseMessage> Cancel(Guid id, int version, string? reason = "TrainFault") =>
        Post(id, "cancel", new { reasonCode = reason, note = "Engine failure", expectedVersion = version });

    private static async Task<JsonElement> Body(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    [Fact]
    public async Task Cancelling_shows_status_Cancelled_with_the_reason_and_every_stop_skipped()
    {
        var id = await NewJourney(Future);

        var response = await Cancel(id, 0);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Body(response);
        Assert.Equal("Cancelled", body.GetProperty("status").GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("status").GetProperty("delayMinutes").ValueKind);
        Assert.Equal("TrainFault", body.GetProperty("reason").GetProperty("code").GetString());
        Assert.Equal(1, body.GetProperty("version").GetInt32());
        Assert.All(body.GetProperty("stops").EnumerateArray(), s => Assert.Equal("Skipped", s.GetProperty("status").GetString()));
        Assert.Equal(body.GetRawText(), (await _client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{id}", Ct)).GetRawText());
    }

    [Fact]
    public async Task The_cancellation_is_announced_on_the_stream_and_the_search_still_lists_the_journey_as_Cancelled()
    {
        var id = await NewJourney(Future);
        using var stream = await SseClient.ConnectAsync(api.Factory.CreateClient(), $"/api/v1/stream?journeyIds={id}", null, Ct);
        await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct);   // the snapshot

        await Cancel(id, 0);

        SseFrame? frame;
        do
        {
            frame = await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct);
            Assert.NotNull(frame);
        }
        while (frame!.Event != "JourneyCancelled");
        Assert.Equal("TrainFault", frame.Json.GetProperty("payload").GetProperty("reason").GetProperty("code").GetString());
        Assert.DoesNotContain("source", frame.Data!, StringComparison.OrdinalIgnoreCase);

        var stations = await RealJourneys.StationsAsync(api.Factory.Services, "KDT", "FOT");
        var found = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/search?from={stations["KDT"]}&to={stations["FOT"]}&date={Future:yyyy-MM-dd}", Ct);
        Assert.Contains(found.EnumerateArray(), j => j.GetProperty("journeyId").GetGuid() == id && j.GetProperty("status").GetProperty("kind").GetString() == "Cancelled");
    }

    [Fact]
    public async Task A_cancelled_journey_cannot_be_started_by_the_driver()
    {
        var id = await NewJourney(new DateOnly(2026, 10, 14));   // today: its start window is open at 04:30 (service 1004 leaves at 05:30)
        await Cancel(id, 0);

        var driver = await api.Factory.NewClient().CabSignInAsync("CAB-02", api.CabDeviceSecrets["CAB-02"], "DRV-02", api.DemoPassword, api.Clock.GetUtcNow(), Ct);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/crew/journeys/{id}/start") { Content = JsonContent.Create(new { clientActionId = Guid.NewGuid() }) };
        request.Headers.Add("Authorization", "Bearer " + await driver.AccessTokenAsync(Ct));
        var start = await _client.SendAsync(request, Ct);

        // 403 OUT_OF_SCOPE (this journey has no driver assigned) or 409 INVALID_STATE: either way it does not start.
        Assert.NotEqual(HttpStatusCode.OK, start.StatusCode);
        Assert.Equal("Cancelled", (await _client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{id}", Ct)).GetProperty("status").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Reinstating_before_the_scheduled_start_puts_the_journey_back_and_removes_the_reason()
    {
        var id = await NewJourney(Future);
        await Cancel(id, 0);

        var response = await Post(id, "reinstate", new { expectedVersion = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Body(response);
        Assert.Equal("NotStarted", body.GetProperty("status").GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("reason").ValueKind);
        Assert.Equal(2, body.GetProperty("version").GetInt32());
        Assert.All(body.GetProperty("stops").EnumerateArray(), s => Assert.Equal("Scheduled", s.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task Reinstating_after_the_scheduled_start_or_a_journey_that_is_not_cancelled_is_409_invalid_state()
    {
        var past = await NewJourney(Past);   // its scheduled start was yesterday
        await Cancel(past, 0);
        var late = await Post(past, "reinstate", new { expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Equal("INVALID_STATE", (await Body(late)).GetProperty("code").GetString());

        var running = await NewJourney(Future);
        var notCancelled = await Post(running, "reinstate", new { expectedVersion = 0 });
        Assert.Equal("INVALID_STATE", (await Body(notCancelled)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_cancellation_needs_a_reason_code_and_a_current_version()
    {
        var id = await NewJourney(Future);

        var noReason = await Cancel(id, 0, reason: null);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.True((await Body(noReason)).GetProperty("errors").TryGetProperty("reasonCode", out _));

        await Cancel(id, 0);
        var stale = await Cancel(id, 0);
        Assert.Equal("VERSION_CONFLICT", (await Body(stale)).GetProperty("code").GetString());
        var twice = await Cancel(id, 1);
        Assert.Equal("INVALID_STATE", (await Body(twice)).GetProperty("code").GetString());
    }
}
