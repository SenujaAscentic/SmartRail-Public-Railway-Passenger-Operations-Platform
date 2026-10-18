using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmartRail.IntegrationTests.Identity;
using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// POST /ops/journeys/{id}/terminate (contract 3.3, US-O4, R-14). Service 1004 runs Kandy (KDT), Peradeniya (PDA), ... Fort.
[Collection("Api")]
public class TerminateDecisionTests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Day = new(2027, 3, 8);

    private readonly HttpClient _client = api.Factory.NewClient();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    private Task<Guid> Running(DateOnly? day = null, Action<OperationalJourney>? more = null) =>
        RealJourneys.CreateAsync(api.Factory.Services, "1004", day ?? Day, j =>
        {
            j.Start(j.Stops[0].ScheduledDeparture!.Value, Guid.NewGuid(), Guid.NewGuid());
            more?.Invoke(j);
        });

    private async Task<Guid> Station(string code) => (await RealJourneys.StationsAsync(api.Factory.Services, code))[code];

    private async Task<HttpResponseMessage> Terminate(Guid id, Guid? station, int version, string? reason = "TrainFault")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ops/journeys/{id}/terminate")
        {
            Content = JsonContent.Create(new { atStationId = station, reasonCode = reason, note = "Engine failure", expectedVersion = version }),
        };
        request.Headers.Add("Authorization", "Bearer " + await api.TokenForAsync("Controller", Ct));
        return await _client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> Body(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    [Fact]
    public async Task Terminating_a_running_journey_shows_TerminatedEarly_with_the_stops_after_the_station_skipped()
    {
        var id = await Running();
        var peradeniya = await Station("PDA");

        var response = await Terminate(id, peradeniya, 0);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Body(response);
        Assert.Equal("TerminatedEarly", body.GetProperty("status").GetProperty("kind").GetString());
        Assert.Equal("TrainFault", body.GetProperty("reason").GetProperty("code").GetString());
        Assert.Equal(1, body.GetProperty("version").GetInt32());
        var stops = body.GetProperty("stops").EnumerateArray().ToList();
        Assert.Equal("Scheduled", stops[1].GetProperty("status").GetString());   // Peradeniya: the last stop now
        Assert.All(stops.Skip(2), s => Assert.Equal("Skipped", s.GetProperty("status").GetString()));
        Assert.Equal(body.GetRawText(), (await _client.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{id}", Ct)).GetRawText());
    }

    [Fact]
    public async Task The_termination_is_announced_with_its_station_and_reason_and_the_journey_end_follows()
    {
        var id = await Running();
        var peradeniya = await Station("PDA");
        using var stream = await SseClient.ConnectAsync(api.Factory.CreateClient(), $"/api/v1/stream?journeyIds={id}", null, Ct);
        await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct);

        await Terminate(id, peradeniya, 0);

        var types = new List<string>();
        JsonElement terminated = default;
        while (!types.Contains("JourneyEnded"))
        {
            var frame = await stream.NextEventAsync(TimeSpan.FromSeconds(5), Ct);
            Assert.NotNull(frame);
            types.Add(frame!.Event!);
            if (frame.Event == "JourneyTerminatedEarly")
            {
                terminated = frame.Json;
            }
        }

        Assert.Equal(peradeniya, terminated.GetProperty("payload").GetProperty("atStationId").GetGuid());
        Assert.Equal("TrainFault", terminated.GetProperty("payload").GetProperty("reason").GetProperty("code").GetString());
        Assert.DoesNotContain("source", terminated.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_station_the_train_has_passed_a_journey_that_is_not_running_and_a_stale_version_are_refused()
    {
        var kandy = await Station("KDT");
        var passed = await Running(Day, j => j.RecordCrewTime(kandy, StopEventKind.Departure, j.Stops[0].ScheduledDeparture!.Value));
        var behind = await Terminate(passed, kandy, 0);
        Assert.Equal(HttpStatusCode.Conflict, behind.StatusCode);
        Assert.Equal("INVALID_STATE", (await Body(behind)).GetProperty("code").GetString());

        var planned = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Day.AddDays(1));
        Assert.Equal("INVALID_STATE", (await Body(await Terminate(planned, await Station("PDA"), 0))).GetProperty("code").GetString());

        var stale = await Terminate(passed, await Station("PDA"), 4);
        Assert.Equal("VERSION_CONFLICT", (await Body(stale)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Terminating_needs_a_station_that_is_on_the_journey_and_a_reason_code()
    {
        var id = await Running();

        var unknown = await Terminate(id, Guid.NewGuid(), 0);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.True((await Body(unknown)).GetProperty("errors").TryGetProperty("stationId", out _));

        var none = await Terminate(id, null, 0, reason: null);
        var errors = (await Body(none)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("atStationId", out _));
        Assert.True(errors.TryGetProperty("reasonCode", out _));
    }
}
