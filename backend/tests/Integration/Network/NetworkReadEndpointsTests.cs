using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.Network;
using Xunit;

namespace SmartRail.IntegrationTests.Network;

// GET /api/v1/lines, GET /api/v1/stations/{id} (contract 3.1, decided shapes 17 Oct) and the INetworkCatalog door.
[Collection("Api")]
public class NetworkReadEndpointsTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _client = api.Factory.CreateClient();

    private async Task<JsonElement> GetJson(string path)
    {
        var response = await _client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private async Task<Guid> StationId(string name)
    {
        var rows = await GetJson("/api/v1/stations?query=" + Uri.EscapeDataString(name));
        return rows.EnumerateArray().First(r => r.GetProperty("name").GetString() == name).GetProperty("id").GetGuid();
    }

    // ---- GET /lines ----

    [Fact]
    public async Task Lines_lists_the_main_line_with_an_up_and_a_down_route()
    {
        var lines = (await GetJson("/api/v1/lines")).EnumerateArray().ToList();

        var main = Assert.Single(lines);
        Assert.Equal("MAIN", main.GetProperty("code").GetString());
        Assert.NotEqual(Guid.Empty, main.GetProperty("lineId").GetGuid());
        var routes = main.GetProperty("routes").EnumerateArray().ToList();
        Assert.Equal(["Down", "Up"], routes.Select(r => r.GetProperty("direction").GetString()));   // ordered by route code
    }

    [Fact]
    public async Task Each_route_lists_its_stations_in_order_with_growing_distance()
    {
        var main = (await GetJson("/api/v1/lines")).EnumerateArray().Single();

        foreach (var route in main.GetProperty("routes").EnumerateArray())
        {
            var stations = route.GetProperty("stations").EnumerateArray().ToList();
            Assert.Equal(11, stations.Count);
            var distances = stations.Select(s => s.GetProperty("distanceKm").GetDecimal()).ToList();
            Assert.Equal(0m, distances[0]);
            Assert.Equal(distances.Order(), distances);
            Assert.All(stations, s =>
            {
                Assert.False(string.IsNullOrEmpty(s.GetProperty("name").GetString()));
                Assert.InRange(s.GetProperty("latitude").GetDouble(), 5, 10);     // Sri Lanka
                Assert.InRange(s.GetProperty("longitude").GetDouble(), 79, 82);
            });
        }
    }

    [Fact]
    public async Task The_up_route_runs_from_Fort_to_Kandy_and_the_down_route_back()
    {
        var routes = (await GetJson("/api/v1/lines")).EnumerateArray().Single().GetProperty("routes").EnumerateArray().ToDictionary(
            r => r.GetProperty("direction").GetString()!, r => r.GetProperty("stations").EnumerateArray().Select(s => s.GetProperty("name").GetString()).ToList());

        Assert.Equal(["Colombo Fort", "Kandy"], [routes["Up"].First(), routes["Up"].Last()]);
        Assert.Equal(["Kandy", "Colombo Fort"], [routes["Down"].First(), routes["Down"].Last()]);
    }

    // ---- GET /stations/{id} ----

    [Fact]
    public async Task A_station_has_its_platforms_in_number_order()
    {
        var fort = await GetJson("/api/v1/stations/" + await StationId("Colombo Fort"));

        Assert.Equal("FOT", fort.GetProperty("code").GetString());
        var platforms = fort.GetProperty("platforms").EnumerateArray().ToList();
        Assert.Equal(Enumerable.Range(1, 10).Select(n => n.ToString()), platforms.Select(p => p.GetProperty("number").GetString()));
        Assert.All(platforms, p => Assert.True(p.GetProperty("inService").GetBoolean()));
        Assert.Equal(JsonValueKind.Number, fort.GetProperty("latitude").ValueKind);
    }

    [Fact]
    public async Task Only_the_stations_the_seed_marks_are_catering_stations()
    {
        var found = new List<string>();
        foreach (var name in new[] { "Colombo Fort", "Maradana", "Polgahawela", "Peradeniya", "Kandy" })
        {
            var station = await GetJson("/api/v1/stations/" + await StationId(name));
            if (station.GetProperty("cateringStation").GetBoolean())
            {
                found.Add(name);
            }
        }

        Assert.Equal(["Peradeniya", "Polgahawela"], found.Order());
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    [InlineData("not-an-id")]
    public async Task An_unknown_or_malformed_station_id_is_404_NOT_FOUND(string id)
    {
        var response = await _client.GetAsync("/api/v1/stations/" + id, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("NOT_FOUND", body.GetProperty("code").GetString());
        Assert.True(body.TryGetProperty("requestId", out _));
    }

    // ---- INetworkCatalog (the door LiveOperations uses) ----

    [Fact]
    public async Task The_catalog_answers_station_names_and_leaves_unknown_ids_out()
    {
        var kandy = await StationId("Kandy");
        using var scope = api.Factory.Services.CreateScope();

        var names = await scope.ServiceProvider.GetRequiredService<INetworkCatalog>()
            .GetStationNamesAsync([kandy, Guid.NewGuid()], Ct);

        Assert.Equal("Kandy", Assert.Single(names).Value);
    }

    [Fact]
    public async Task The_catalog_describes_a_train_set_and_its_coaches()
    {
        using var scope = api.Factory.Services.CreateScope();
        var schedule = await scope.ServiceProvider.GetRequiredService<INetworkSchedule>().GetScheduleAsync(Ct);
        var id = schedule.TrainSets.Single(t => t.Code == "DPS-01").Id;
        var catalog = scope.ServiceProvider.GetRequiredService<INetworkCatalog>();

        var trainSet = await catalog.GetTrainSetAsync(id, Ct);

        Assert.Equal("DPS-01", trainSet!.Code);
        Assert.Equal("DieselPowerSet", trainSet.Traction);
        Assert.Equal([1, 2, 3, 4, 5], trainSet.Coaches.Select(c => c.Position));
        Assert.Equal(new CoachDetails(1, "First", "Reserved"), trainSet.Coaches[0]);
        Assert.Null(await catalog.GetTrainSetAsync(Guid.NewGuid(), Ct));
    }
}
