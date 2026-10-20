using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.IntegrationTests.Identity;
using SmartRail.Identity;
using SmartRail.Simulator;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// Gate G2, end to end: the simulator drives a whole journey through the public API, and a client connected to /stream sees it happen.
// Service 1004 (Kandy to Fort, DRV-02, tablet CAB-02) leaves at 05:30 UTC and the test clock is 04:30, so the start window is open.
// The simulator runs on its own clock (InstantSimulatorTime), so a three-hour journey takes seconds and the test clock stays put.
[Collection("Api")]
public class SimulatorEndToEndTests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Today = new(2026, 10, 14);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    private async Task<Guid> JourneyAsync()
    {
        Guid driver;
        using (var scope = api.Factory.Services.CreateScope())
        {
            driver = (await scope.ServiceProvider.GetRequiredService<IStaffDirectory>().GetDriverIdsAsync(["DRV-02"], Ct))["DRV-02"];
        }

        return await RealJourneys.CreateAsync(api.Factory.Services, "1004", Today, j => j.AssignDriver(driver));
    }

    private TrainSimulator Simulator(HttpClient http, Guid journeyId, Scenario scenario, Action<string> log) =>
        new(http,
            new SimulatorOptions
            {
                ApiBase = http.BaseAddress!,
                DeviceId = "CAB-02",
                DeviceSecret = api.CabDeviceSecrets["CAB-02"],
                StaffNumber = "DRV-02",
                Password = api.DemoPassword,
                JourneyId = journeyId,
                Scenario = scenario,
                TickSeconds = 30,
            },
            serverNow => new InstantSimulatorTime(serverNow), log);

    // Runs the simulator while a stream client records every event; returns the events and how long the last one took to arrive.
    private async Task<(List<(string Type, JsonElement Payload)> Events, TimeSpan LastEventDelay, SimulationResult Result)> RunAsync(Scenario scenario)
    {
        var journeyId = await JourneyAsync();
        var http = api.Factory.CreateClient();
        using var client = await SseClient.ConnectAsync(http, $"/api/v1/stream?journeyIds={journeyId}", lastEventId: null, Ct);
        Assert.Equal("snapshot", (await client.NextEventAsync(TimeSpan.FromSeconds(5), Ct))!.Event);

        var ended = new Stopwatch();
        var run = Task.Run(() => Simulator(http, journeyId, scenario, line => { if (line.StartsWith("Journey ended")) ended.Start(); }).RunAsync(Ct), Ct);

        var events = new List<(string, JsonElement)>();
        var delay = TimeSpan.Zero;
        while (true)
        {
            var frame = await client.NextEventAsync(TimeSpan.FromSeconds(20), Ct);
            if (frame is null && run.IsFaulted)
            {
                await run;   // shows why the simulator stopped
            }

            Assert.True(frame is not null, "The stream went quiet before the journey ended.");
            events.Add((frame!.Event!, frame.Json.Clone()));
            if (frame.Event == "JourneyEnded")
            {
                delay = ended.Elapsed;
                break;
            }
        }

        return (events, delay, await run);
    }

    [Fact]
    public async Task A_normal_run_starts_moves_through_every_station_and_completes_and_the_stream_shows_it()
    {
        var (events, lastEventDelay, result) = await RunAsync(Scenario.Normal);

        Assert.True(result.Accepted > 100, $"Only {result.Accepted} fixes were accepted.");
        Assert.Equal(0, result.Discarded);

        var types = events.Select(e => e.Type).ToList();
        Assert.Equal("JourneyStarted", types.First(t => t != "snapshot"));
        Assert.Contains("TrainPositionUpdated", types);
        Assert.Equal("JourneyEnded", types.Last());
        Assert.True(lastEventDelay < TimeSpan.FromSeconds(5), $"JourneyEnded took {lastEventDelay.TotalSeconds:F1} s to reach the client (limit 5 s, G2).");

        var statuses = events.Where(e => e.Type == "StopStatusChanged").Select(e => e.Payload.GetProperty("payload").GetProperty("to").GetString()).ToList();
        Assert.Contains("Departed", statuses);
        Assert.Contains("Arrived", statuses);

        var detail = await api.Factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{result.JourneyId}", Ct);
        Assert.Equal("Completed", detail.GetProperty("status").GetProperty("kind").GetString());
        Assert.All(detail.GetProperty("stops").EnumerateArray(), s => Assert.NotEqual("Scheduled", s.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task A_run_with_a_halt_between_stations_still_completes_and_the_train_is_late_afterwards()
    {
        var (events, _, result) = await RunAsync(Scenario.Stop);

        Assert.Equal(0, result.Discarded);
        Assert.Contains(events, e => e.Type == "DelayChanged");
        Assert.Equal("JourneyEnded", events.Last().Type);
    }

    [Fact]
    public async Task A_held_train_waits_at_the_station_until_the_controller_releases_it_and_the_hold_is_logged()
    {
        var journeyId = await JourneyAsync();
        var http = api.Factory.CreateClient();
        var token = await api.TokenForAsync("Controller", Ct);

        async Task<HttpResponseMessage> Decide(string path, object body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ops/journeys/{journeyId}/{path}") { Content = JsonContent.Create(body) };
            request.Headers.Add("Authorization", "Bearer " + token);
            return await http.SendAsync(request, Ct);
        }

        // The second stop of the journey is held before the train even starts.
        var before = await http.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{journeyId}", Ct);
        var held = before.GetProperty("stops")[1];
        var scheduledDeparture = held.GetProperty("scheduledDeparture").GetDateTimeOffset();
        Assert.Equal(HttpStatusCode.OK, (await Decide("hold", new { stationId = held.GetProperty("stationId").GetGuid(), minutes = 10, expectedVersion = 0 })).StatusCode);

        var lines = new List<string>();
        InstantSimulatorTime? trainClock = null;
        var simulator = new TrainSimulator(http,
            new SimulatorOptions
            {
                ApiBase = http.BaseAddress!, DeviceId = "CAB-02", DeviceSecret = api.CabDeviceSecrets["CAB-02"], StaffNumber = "DRV-02",
                Password = api.DemoPassword, JourneyId = journeyId, TickSeconds = 30,
            },
            serverNow => trainClock = new InstantSimulatorTime(serverNow),
            line => { lock (lines) lines.Add(line); });
        var run = Task.Run(() => simulator.RunAsync(Ct), Ct);

        // Wait until the tablet has been standing at the held station for 10 minutes of its own clock, then release.
        var deadline = Stopwatch.StartNew();
        while (!(trainClock is { } clock && clock.Now >= scheduledDeparture.AddMinutes(10)))
        {
            Assert.True(deadline.Elapsed < TimeSpan.FromSeconds(60), "The train never reached the held station.");
            Assert.False(run.IsCompleted, "The train finished although it was held.");
            await Task.Delay(50, Ct);
        }

        var detail = await http.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{journeyId}", Ct);
        Assert.NotEqual(JsonValueKind.Null, detail.GetProperty("hold").ValueKind);   // still held after its scheduled departure
        Assert.Equal("Arrived", detail.GetProperty("stops")[1].GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Decide("hold/release", new { expectedVersion = detail.GetProperty("version").GetInt32() })).StatusCode);

        var result = await run;
        Assert.Equal(0, result.Discarded);
        lock (lines)
        {
            Assert.Contains(lines, l => l.StartsWith("Hold received"));
            Assert.Contains(lines, l => l.StartsWith("Hold released"));
            Assert.True(lines.FindIndex(l => l.StartsWith("Hold received")) < lines.FindIndex(l => l.StartsWith("Hold released")));
        }

        var done = await http.GetFromJsonAsync<JsonElement>($"/api/v1/journeys/{journeyId}", Ct);
        Assert.Equal("Completed", done.GetProperty("status").GetProperty("kind").GetString());
        var actualDeparture = done.GetProperty("stops")[1].GetProperty("actualDeparture").GetDateTimeOffset();
        Assert.True(actualDeparture >= scheduledDeparture.AddMinutes(10), $"It left at {actualDeparture:HH:mm}, but was held until after {scheduledDeparture.AddMinutes(10):HH:mm}.");
    }
}
