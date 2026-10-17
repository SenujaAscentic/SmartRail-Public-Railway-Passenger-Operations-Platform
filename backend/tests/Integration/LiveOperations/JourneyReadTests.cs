using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmartRail.LiveOperations.Domain;
using Xunit;

namespace SmartRail.IntegrationTests.LiveOperations;

// GET /api/v1/journeys/search and GET /api/v1/journeys/{id} (contract 3.1, 5). Every test makes the journeys it needs from real
// seeded services on a date of its own in 2027, so it never depends on the generated journeys or on another test.
[Collection("Api")]
public class JourneyReadTests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // 2027-03-01 is a Monday; each test uses its own day.
    private static DateOnly Day(int n) => new(2027, 3, 1 + n);

    private readonly HttpClient _client = api.Factory.CreateClient();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    private async Task<Guid> Station(string code) => (await RealJourneys.StationsAsync(api.Factory.Services, code))[code];

    private async Task<List<JsonElement>> Search(string from, string to, DateOnly date, string extra = "")
    {
        var url = $"/api/v1/journeys/search?from={await Station(from)}&to={await Station(to)}&date={date:yyyy-MM-dd}{extra}";
        var response = await _client.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).EnumerateArray().ToList();
    }

    private static List<string> Numbers(IEnumerable<JsonElement> rows) => rows.Select(r => r.GetProperty("serviceNumber").GetString()!).ToList();

    // A local Colombo time on a date as a UTC instant (service times are minutes after local midnight).
    private static DateTime At(DateOnly date, int hour, int minute) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified), TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo"));

    // The same instant as text for the `after` parameter.
    private static string Utc(DateOnly date, int hour, int minute) => At(date, hour, minute).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    // ---- search ----

    [Fact]
    public async Task Search_lists_the_trains_that_stop_at_both_stations_sorted_by_departure()
    {
        var day = Day(0);
        await RealJourneys.CreateAsync(api.Factory.Services, "1001", day);   // Express, leaves Fort 06:30
        await RealJourneys.CreateAsync(api.Factory.Services, "2001", day);   // Slow, leaves Fort 06:00

        var rows = await Search("FOT", "KDT", day);

        Assert.Equal(["2001", "1001"], Numbers(rows));
        var slow = rows[0];
        Assert.Equal("Kandy Slow", slow.GetProperty("name").GetString());
        Assert.Equal("Slow", slow.GetProperty("trainType").GetString());
        Assert.Equal(day.ToString("yyyy-MM-dd"), slow.GetProperty("serviceDate").GetString());
        Assert.Equal("Colombo Fort", slow.GetProperty("from").GetProperty("name").GetString());
        Assert.Equal("Kandy", slow.GetProperty("to").GetProperty("name").GetString());
        Assert.Equal(DateTime.Parse(Utc(day, 6, 0)).ToUniversalTime(), slow.GetProperty("from").GetProperty("scheduledDeparture").GetDateTime().ToUniversalTime());
        Assert.Equal(DateTime.Parse(Utc(day, 9, 50)).ToUniversalTime(), slow.GetProperty("to").GetProperty("scheduledArrival").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task A_fresh_journey_has_no_data_so_it_is_NotStarted_and_the_later_phase_fields_are_present_but_null()
    {
        var day = Day(1);
        await RealJourneys.CreateAsync(api.Factory.Services, "2001", day);

        var row = (await Search("FOT", "KDT", day)).Single();

        Assert.Equal("NotStarted", row.GetProperty("status").GetProperty("kind").GetString());
        Assert.Equal("NotStarted", row.GetProperty("freshness").GetProperty("tracking").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("freshness").GetProperty("updatedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("hold").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("reason").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("from").GetProperty("previousPlatform").ValueKind);
        Assert.Equal(0, row.GetProperty("version").GetInt32());
        Assert.Equal("4", row.GetProperty("from").GetProperty("platform").GetString());   // planned platform of 2001 at Fort (services.json)
    }

    [Fact]
    public async Task An_express_that_only_passes_a_station_is_not_listed_for_it()
    {
        var day = Day(2);
        await RealJourneys.CreateAsync(api.Factory.Services, "1001", day);   // passes Kelaniya
        await RealJourneys.CreateAsync(api.Factory.Services, "2001", day);   // stops at Kelaniya

        Assert.Equal(["2001"], Numbers(await Search("FOT", "KLA", day)));
    }

    [Fact]
    public async Task Search_only_finds_trains_that_run_from_the_first_station_towards_the_second()
    {
        var day = Day(3);
        await RealJourneys.CreateAsync(api.Factory.Services, "2001", day);   // Fort to Kandy
        await RealJourneys.CreateAsync(api.Factory.Services, "2002", day);   // Kandy to Fort

        Assert.Equal(["2001"], Numbers(await Search("FOT", "KDT", day)));
        Assert.Equal(["2002"], Numbers(await Search("KDT", "FOT", day)));
        Assert.Equal(["2001"], Numbers(await Search("MDA", "PDA", day)));      // between intermediate stations, up
        Assert.Equal(["2002"], Numbers(await Search("PDA", "MDA", day)));      // and down
    }

    [Fact]
    public async Task Only_trains_running_on_the_date_are_listed()
    {
        await RealJourneys.CreateAsync(api.Factory.Services, "2001", Day(4));

        Assert.Empty(await Search("FOT", "KDT", Day(5)));
        Assert.Single(await Search("FOT", "KDT", Day(4)));
    }

    [Fact]
    public async Task The_limit_keeps_the_earliest_departures()
    {
        var day = Day(6);
        await RealJourneys.CreateAsync(api.Factory.Services, "1001", day);
        await RealJourneys.CreateAsync(api.Factory.Services, "2001", day);

        Assert.Equal(["2001"], Numbers(await Search("FOT", "KDT", day, "&limit=1")));
    }

    [Fact]
    public async Task Departures_before_the_after_time_are_hidden()
    {
        var day = Day(7);
        await RealJourneys.CreateAsync(api.Factory.Services, "1001", day);   // 06:30
        await RealJourneys.CreateAsync(api.Factory.Services, "2001", day);   // 06:00

        Assert.Equal(["1001"], Numbers(await Search("FOT", "KDT", day, "&after=" + Uri.EscapeDataString(Utc(day, 6, 15)))));
    }

    // The EXPECTED departure decides, so a late train that has not left yet stays in the list.
    [Fact]
    public async Task A_late_train_that_has_not_left_is_still_listed_after_its_scheduled_time()
    {
        var day = Day(8);
        await RealJourneys.CreateAsync(api.Factory.Services, "2001", day, j => j.SetControllerDelay(new Delay(10), At(day, 5, 0)));   // 06:00 + 10 min

        var after = "&after=" + Uri.EscapeDataString(Utc(day, 6, 5));
        var row = Assert.Single(await Search("FOT", "KDT", day, after));
        Assert.Equal("Late", row.GetProperty("status").GetProperty("kind").GetString());
        Assert.Equal(10, row.GetProperty("status").GetProperty("delayMinutes").GetInt32());
        Assert.Equal(
            DateTime.Parse(Utc(day, 6, 10)).ToUniversalTime(), row.GetProperty("from").GetProperty("expectedDeparture").GetDateTime().ToUniversalTime());
        Assert.Empty(await Search("FOT", "KDT", day, "&after=" + Uri.EscapeDataString(Utc(day, 6, 15))));
    }

    // A train that has already left the departure station cannot be caught, whatever the time says.
    [Fact]
    public async Task A_train_that_has_already_left_the_departure_station_is_hidden()
    {
        var day = Day(9);
        var stations = await RealJourneys.StationsAsync(api.Factory.Services, "FOT", "MDA");
        await RealJourneys.CreateAsync(api.Factory.Services, "2001", day, j => j.RecordCrewTime(stations["MDA"], StopEventKind.Arrival, At(day, 6, 3)));

        Assert.Empty(await Search("FOT", "KDT", day));                // Fort: left (inferred)
        Assert.Single(await Search("MDA", "KDT", day));               // Maradana: the train is there and has not left
    }

    [Fact]
    public async Task Today_past_departures_are_hidden_by_default()
    {
        // The clock is 10:00 in Colombo (04:30 UTC). The generated journeys of today are not touched by this test.
        var from = await Station("KDT");
        var to = await Station("FOT");
        var response = await _client.GetAsync($"/api/v1/journeys/search?from={from}&to={to}", Ct);   // date defaults to today
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rows = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).EnumerateArray().ToList();

        Assert.All(rows, r =>
            Assert.True(r.GetProperty("from").GetProperty("expectedDeparture").GetDateTime().ToUniversalTime() >= api.Clock.GetUtcNow().UtcDateTime));
    }

    [Theory]
    [InlineData("", "from")]
    [InlineData("from=x&to=y", "from")]
    [InlineData("from={fort}&to={kandy}&limit=0", "limit")]
    [InlineData("from={fort}&to={kandy}&date=tomorrow", "date")]
    public async Task A_bad_request_is_400_with_field_errors(string query, string field)
    {
        query = query.Replace("{fort}", (await Station("FOT")).ToString()).Replace("{kandy}", (await Station("KDT")).ToString());

        var response = await _client.GetAsync("/api/v1/journeys/search?" + query, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
        Assert.Contains(field, body.GetProperty("errors").EnumerateObject().Select(p => p.Name));
        Assert.True(body.TryGetProperty("requestId", out _));
    }

    [Fact]
    public async Task The_same_station_twice_is_400_and_an_unknown_station_is_404()
    {
        var fort = await Station("FOT");

        var same = await _client.GetAsync($"/api/v1/journeys/search?from={fort}&to={fort}", Ct);
        var unknown = await _client.GetAsync($"/api/v1/journeys/search?from={fort}&to={Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("NOT_FOUND", (await unknown.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }

    // ---- detail ----

    private async Task<JsonElement> Detail(Guid id)
    {
        var response = await _client.GetAsync("/api/v1/journeys/" + id, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    [Fact]
    public async Task The_detail_has_the_summary_fields_all_eleven_stops_and_the_train_set()
    {
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1001", Day(10));

        var detail = await Detail(id);

        Assert.Equal(id, detail.GetProperty("journeyId").GetGuid());
        Assert.Equal("1001", detail.GetProperty("serviceNumber").GetString());
        Assert.Equal("Kandy Express", detail.GetProperty("name").GetString());
        Assert.Equal("Colombo Fort", detail.GetProperty("from").GetProperty("name").GetString());
        Assert.Equal("Kandy", detail.GetProperty("to").GetProperty("name").GetString());
        var stops = detail.GetProperty("stops").EnumerateArray().ToList();
        Assert.Equal(Enumerable.Range(1, 11), stops.Select(s => s.GetProperty("sequence").GetInt32()));
        Assert.Equal("Colombo Fort", stops[0].GetProperty("name").GetString());
        Assert.Equal("Pass", stops[2].GetProperty("callType").GetString());          // Kelaniya: the express passes
        Assert.Equal("Scheduled", stops[2].GetProperty("status").GetString());
        Assert.False(stops[2].GetProperty("isAdded").GetBoolean());
        Assert.Equal(JsonValueKind.Null, stops[0].GetProperty("scheduledArrival").ValueKind);   // the origin has no arrival
        Assert.Equal(JsonValueKind.Null, stops[10].GetProperty("scheduledDeparture").ValueKind); // the terminus has no departure
        var trainSet = detail.GetProperty("trainSet");
        Assert.Equal("DPS-01", trainSet.GetProperty("code").GetString());
        Assert.Equal("DieselPowerSet", trainSet.GetProperty("traction").GetString());
        Assert.Equal(5, trainSet.GetProperty("coaches").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("freshness").GetProperty("lastPosition").ValueKind);
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("hold").ValueKind);
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("reason").ValueKind);
        Assert.Equal(0, detail.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task The_detail_shows_what_has_happened_and_moves_the_later_expected_times()
    {
        var stations = await RealJourneys.StationsAsync(api.Factory.Services, "MDA");
        var arrival = At(Day(11), 6, 3).AddMinutes(4);   // 2001 is due at Maradana 06:03 local
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "2001", Day(11), j => j.RecordCrewTime(stations["MDA"], StopEventKind.Arrival, arrival));

        var detail = await Detail(id);

        Assert.Equal("Late", detail.GetProperty("status").GetProperty("kind").GetString());
        Assert.Equal(4, detail.GetProperty("status").GetProperty("delayMinutes").GetInt32());
        var stops = detail.GetProperty("stops").EnumerateArray().ToList();
        Assert.Equal("Departed", stops[0].GetProperty("status").GetString());   // inferred, no time
        Assert.Equal("Arrived", stops[1].GetProperty("status").GetString());
        Assert.Equal(arrival, stops[1].GetProperty("actualArrival").GetDateTime().ToUniversalTime());
        var scheduledNext = stops[2].GetProperty("scheduledArrival").GetDateTime();
        Assert.Equal(scheduledNext.AddMinutes(4), stops[2].GetProperty("expectedArrival").GetDateTime());   // R-02
    }

    // T-07 on the real endpoint: the time at Maradana came from a crew tap, and nothing about that is visible.
    [Fact]
    public async Task The_detail_shows_no_source_and_no_staff_fields()
    {
        var stations = await RealJourneys.StationsAsync(api.Factory.Services, "MDA");
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "2001", Day(12), j => j.RecordCrewTime(stations["MDA"], StopEventKind.Arrival, At(Day(12), 6, 3)));

        var text = await (await _client.GetAsync("/api/v1/journeys/" + id, Ct)).Content.ReadAsStringAsync(Ct);

        foreach (var forbidden in new[] { "source", "crew", "actor", "deviceId", "driver", "incident" })
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    [InlineData("not-an-id")]
    public async Task An_unknown_or_malformed_journey_id_is_404_NOT_FOUND(string id)
    {
        var response = await _client.GetAsync("/api/v1/journeys/" + id, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("NOT_FOUND", (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }
}
