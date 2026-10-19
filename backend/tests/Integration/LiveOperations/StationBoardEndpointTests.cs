using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SmartRail.IntegrationTests.Identity;
using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// GET /stations/{stationId}/board (contract 3.1, US-D1). The test clock is 14 Oct 2026, 04:30 UTC = 10:00 in Colombo. Service 1004
// (weekend, so the generator made none today) leaves Kandy at 11:00 local, calls at Peradeniya 11:05-11:07 and ends in Fort.
[Collection("Api")]
public class StationBoardEndpointTests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Today = new(2026, 10, 14);

    private readonly HttpClient _client = api.Factory.NewClient();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    private async Task<Guid> Station(string code) => (await RealJourneys.StationsAsync(api.Factory.Services, code))[code];

    private async Task<JsonElement> Board(Guid station, string query = "") =>
        await _client.GetFromJsonAsync<JsonElement>($"/api/v1/stations/{station}/board{query}", Ct);

    private static JsonElement RowOf(JsonElement board, Guid journeyId) => board.EnumerateArray().Single(r => r.GetProperty("journeyId").GetGuid() == journeyId);

    [Fact]
    public async Task A_departures_board_lists_the_train_with_time_destination_platform_and_status()
    {
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Today);
        var peradeniya = await Station("PDA");

        var row = RowOf(await Board(peradeniya), id);

        Assert.Equal("1004", row.GetProperty("serviceNumber").GetString());
        Assert.Equal("Express", row.GetProperty("trainType").GetString());
        Assert.Equal("Colombo Fort", row.GetProperty("destination").GetProperty("name").GetString());
        Assert.Equal("Kandy", row.GetProperty("origin").GetProperty("name").GetString());
        Assert.Equal(new DateTime(2026, 10, 14, 5, 37, 0, DateTimeKind.Utc), row.GetProperty("time").GetDateTime());   // 11:07 local
        Assert.Equal("2", row.GetProperty("platform").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("previousPlatform").ValueKind);
        Assert.Equal("Stop", row.GetProperty("callType").GetString());
        Assert.Equal("NotStarted", row.GetProperty("status").GetProperty("kind").GetString());
        Assert.False(row.GetProperty("hold").GetBoolean());
        Assert.DoesNotContain("source", row.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_train_that_only_passes_is_listed_with_call_type_Pass()
    {
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Today);

        var row = RowOf(await Board(await Station("KGW")), id);   // Kadugannawa: 1004 passes

        Assert.Equal("Pass", row.GetProperty("callType").GetString());
    }

    [Fact]
    public async Task The_arrivals_board_uses_the_arrival_time_and_the_terminus_has_one()
    {
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Today);
        var fort = await Station("FOT");

        var arrivals = await Board(fort, "?kind=arrivals");
        var departures = await Board(fort, "?kind=departures");

        Assert.Equal(new DateTime(2026, 10, 14, 8, 15, 0, DateTimeKind.Utc), RowOf(arrivals, id).GetProperty("time").GetDateTime());   // 13:45 local
        Assert.DoesNotContain(departures.EnumerateArray(), r => r.GetProperty("journeyId").GetGuid() == id);   // it ends there
    }

    [Fact]
    public async Task A_platform_change_a_hold_and_a_cancellation_show_on_the_board()
    {
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Today);
        var peradeniya = await Station("PDA");
        var controller = await api.TokenForAsync("Controller", Ct);

        async Task Post(string path, object body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            request.Headers.Add("Authorization", "Bearer " + controller);
            Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(request, Ct)).StatusCode);
        }

        await Post($"/api/v1/ops/journeys/{id}/stops/{peradeniya}/platform", new { platform = "3", conflictsAcknowledged = true, expectedVersion = 0 });
        await Post($"/api/v1/ops/journeys/{id}/hold", new { stationId = peradeniya, minutes = 5, expectedVersion = 1 });
        var changed = RowOf(await Board(peradeniya), id);
        Assert.Equal(("3", "2", true), (changed.GetProperty("platform").GetString(), changed.GetProperty("previousPlatform").GetString(), changed.GetProperty("hold").GetBoolean()));

        await Post($"/api/v1/ops/journeys/{id}/cancel", new { reasonCode = "TrainFault", expectedVersion = 2 });
        var cancelled = RowOf(await Board(peradeniya), id);   // its time (11:07) has not passed: it stays
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetProperty("kind").GetString());
        Assert.Equal("TrainFault", cancelled.GetProperty("reason").GetProperty("code").GetString());
        Assert.False(cancelled.GetProperty("hold").GetBoolean());
    }

    [Fact]
    public async Task The_board_shows_only_todays_service_day_and_each_row_says_which_day()
    {
        var today = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Today);
        var tomorrow = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Today.AddDays(1));   // same 11:07 at Peradeniya

        var board = await Board(await Station("PDA"));

        var row = RowOf(board, today);
        Assert.Equal("2026-10-14", row.GetProperty("serviceDate").GetString());
        Assert.DoesNotContain(board.EnumerateArray(), r => r.GetProperty("journeyId").GetGuid() == tomorrow);
    }

    [Fact]
    public async Task Late_in_the_day_with_nothing_left_the_board_is_empty_and_does_not_fall_through_to_tomorrow()
    {
        // 23:30 in Colombo on 14 Oct (18:00 UTC): today's 1004 left long ago, tomorrow's is not shown.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 14, 18, 0, 0, TimeSpan.Zero));
        using var factory = api.CreateFactory(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        await RealJourneys.CreateAsync(factory.Services, "1004", Today);
        await RealJourneys.CreateAsync(factory.Services, "1004", Today.AddDays(1));
        var peradeniya = (await RealJourneys.StationsAsync(factory.Services, "PDA"))["PDA"];

        var board = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/stations/{peradeniya}/board", Ct);

        Assert.Equal(0, board.GetArrayLength());

        clock.Advance(TimeSpan.FromMinutes(31));   // 00:01 on 15 Oct in Colombo: the new service day begins
        var next = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/stations/{peradeniya}/board", Ct);
        Assert.NotEqual(0, next.GetArrayLength());   // the generator's own 15 Oct services are there too
        Assert.All(next.EnumerateArray(), r => Assert.Equal("2026-10-15", r.GetProperty("serviceDate").GetString()));
    }

    [Fact]
    public async Task The_limit_and_the_kind_are_checked_and_an_unknown_station_is_404()
    {
        var kandy = await Station("KDT");

        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/v1/stations/{kandy}/board?kind=sideways", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/v1/stations/{kandy}/board?limit=0", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/stations/{Guid.NewGuid()}/board", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/v1/stations/not-a-guid/board", Ct)).StatusCode);
        Assert.True((await Board(kandy, "?limit=2")).GetArrayLength() <= 2);
    }
}
