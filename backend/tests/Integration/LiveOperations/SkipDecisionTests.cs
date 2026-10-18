using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmartRail.IntegrationTests.Identity;
using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// POST /ops/journeys/{id}/stops/{stationId}/skip (contract 3.3, US-O4, R-15, R-21). Service 1004: Kandy (origin), Peradeniya (stop),
// Kadugannawa (pass), Rambukkana (pass), Polgahawela (stop, dwell 3 min), ... Fort (terminus).
[Collection("Api")]
public class SkipDecisionTests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Day = new(2027, 3, 9);

    private readonly HttpClient _client = api.Factory.NewClient();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    private Task<Guid> Journey(Action<OperationalJourney>? change = null, DateOnly? day = null) =>
        RealJourneys.CreateAsync(api.Factory.Services, "1004", day ?? Day, change);

    private async Task<Guid> Station(string code) => (await RealJourneys.StationsAsync(api.Factory.Services, code))[code];

    private async Task<HttpResponseMessage> Skip(Guid id, Guid station, int version)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ops/journeys/{id}/stops/{station}/skip") { Content = JsonContent.Create(new { expectedVersion = version }) };
        request.Headers.Add("Authorization", "Bearer " + await api.TokenForAsync("Controller", Ct));
        return await _client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> Body(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private static string StatusAt(JsonElement journey, Guid station) =>
        journey.GetProperty("stops").EnumerateArray().Single(s => s.GetProperty("stationId").GetGuid() == station).GetProperty("status").GetString()!;

    [Fact]
    public async Task Skipping_a_stop_marks_it_Skipped_bumps_the_version_and_is_announced()
    {
        var id = await Journey();
        var peradeniya = await Station("PDA");
        using var stream = await SseClient.ConnectAsync(api.Factory.CreateClient(), $"/api/v1/stream?journeyIds={id}", null, Ct);
        await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct);

        var response = await Skip(id, peradeniya, 0);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Body(response);
        Assert.Equal("Skipped", StatusAt(body, peradeniya));
        Assert.Equal(1, body.GetProperty("version").GetInt32());
        Assert.Equal(body.GetRawText(), (await _client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{id}", Ct)).GetRawText());

        SseFrame? frame;
        do
        {
            frame = await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct);
            Assert.NotNull(frame);
        }
        while (frame!.Event != "StopSkipped");
        Assert.Equal(peradeniya, frame.Json.GetProperty("payload").GetProperty("stationId").GetGuid());
    }

    [Fact]
    public async Task Skipping_a_stop_gives_the_later_stops_its_scheduled_dwell_back()
    {
        var polgahawela = await Station("PLG");
        var id = await Journey(j =>
        {
            var pda = j.Stops[1];
            j.RecordCrewTime(pda.StationId, StopEventKind.Arrival, pda.ScheduledArrival!.Value.AddMinutes(10));   // 10 minutes late
        });
        Assert.Equal(10, (await _client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{id}", Ct)).GetProperty("status").GetProperty("delayMinutes").GetInt32());

        var response = await Skip(id, polgahawela, 0);   // Polgahawela dwells 3 minutes

        Assert.Equal(7, (await Body(response)).GetProperty("status").GetProperty("delayMinutes").GetInt32());
    }

    [Fact]
    public async Task Skip_refused_for_origin_station_a_pass_stop_the_terminus_and_a_stop_already_left()
    {
        var kandy = await Station("KDT");
        var id = await Journey();
        foreach (var code in new[] { "KDT", "KGW", "FOT" })
        {
            var refused = await Skip(id, await Station(code), 0);
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal("INVALID_STATE", (await Body(refused)).GetProperty("code").GetString());
        }

        var left = await Journey(j => j.RecordCrewTime(j.Stops[1].StationId, StopEventKind.Departure, j.Stops[1].ScheduledDeparture!.Value), Day.AddDays(1));
        Assert.Equal("INVALID_STATE", (await Body(await Skip(left, await Station("PDA"), 0))).GetProperty("code").GetString());
        Assert.NotEqual(Guid.Empty, kandy);
    }

    [Fact]
    public async Task A_station_that_is_not_a_stop_is_404_and_a_stale_version_is_409()
    {
        var id = await Journey();

        Assert.Equal(HttpStatusCode.NotFound, (await Skip(id, Guid.NewGuid(), 0)).StatusCode);

        var stale = await Skip(id, await Station("PDA"), 5);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("VERSION_CONFLICT", (await Body(stale)).GetProperty("code").GetString());
    }
}
