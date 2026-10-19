using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.CateringIntegration.Persistence;
using SmartRail.IntegrationTests.LiveOperations;
using Xunit;

namespace SmartRail.IntegrationTests.CateringIntegration;

// GET /api/v1/stations/{id}/catering (contract 3.6, US-P8, US-F3): menus pulled from RailBites every 5 minutes into the cache,
// answered from the cache, and never a failure of train information when the provider is down.
[Collection("Api")]
public sealed class CateringEndpointTests(ApiFixture api) : CateringTestBase(api)
{
    private async Task<Guid> StationId(string code) =>
        (await RealJourneys.StationsAsync(_smartRail.Services, code))[code];

    private async Task<JsonElement> Catering(string code) =>
        await _client.GetFromJsonAsync<JsonElement>($"/api/v1/stations/{await StationId(code)}/catering", Ct);

    [Fact]
    public async Task Polgahawela_has_its_menu_hotline_and_hours_in_the_agreed_shape()
    {
        var body = await Catering("PLG");

        Assert.True(body.GetProperty("available").GetBoolean());
        Assert.True(body.GetProperty("openNow").GetBoolean());   // the test clock is 10:00 in Colombo
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("hotline").GetString()));
        Assert.Equal("06:00", body.GetProperty("openingHours").GetProperty("opens").GetString());
        Assert.Equal("20:00", body.GetProperty("openingHours").GetProperty("closes").GetString());
        var menu = body.GetProperty("menu").EnumerateArray().ToList();
        Assert.True(menu.Count >= 3);
        Assert.All(menu, item => Assert.True(item.GetProperty("price").GetInt32() > 0));
        Assert.Contains(menu, item => !item.GetProperty("available").GetBoolean());   // one is sold out
        Assert.Equal(["available", "hotline", "menu", "openNow", "openingHours"], body.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task openNow_is_false_outside_the_hours_and_is_worked_out_by_the_server()
    {
        _clock.Advance(TimeSpan.FromHours(10.5));   // 20:30 in Colombo
        await RunRoundAsync();               // the menus are pulled again (5 minutes have passed), so they are fresh

        var body = await Catering("PLG");

        Assert.True(body.GetProperty("available").GetBoolean());
        Assert.False(body.GetProperty("openNow").GetBoolean());
    }

    [Fact]
    public async Task While_the_provider_is_down_the_last_menu_is_served_for_15_minutes_then_the_menu_is_unavailable_and_train_information_still_works()
    {
        _providerDown = true;

        _clock.Advance(TimeSpan.FromMinutes(6));
        await RunRoundAsync();   // the pull fails; nothing is lost
        Assert.True((await Catering("PLG")).GetProperty("available").GetBoolean());

        _clock.Advance(TimeSpan.FromMinutes(10));
        await RunRoundAsync();
        var body = await Catering("PLG");

        Assert.False(body.GetProperty("available").GetBoolean());
        Assert.Empty(body.GetProperty("menu").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/v1/lines", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/v1/stations/{await StationId("PLG")}", Ct)).StatusCode);
    }

    [Fact]
    public async Task When_the_provider_comes_back_the_next_pull_makes_the_menu_available_again()
    {
        _providerDown = true;
        _clock.Advance(TimeSpan.FromMinutes(20));
        await RunRoundAsync();
        Assert.False((await Catering("PLG")).GetProperty("available").GetBoolean());

        _providerDown = false;
        _clock.Advance(TimeSpan.FromMinutes(6));
        await RunRoundAsync();

        Assert.True((await Catering("PLG")).GetProperty("available").GetBoolean());
    }

    [Fact]
    public async Task Nothing_cached_gives_200_unavailable_with_an_empty_menu_not_an_error()
    {
        using (var scope = _smartRail.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CateringDbContext>();
            db.CachedStations.RemoveRange(db.CachedStations);
            await db.SaveChangesAsync(Ct);
        }

        var body = await Catering("PLG");

        Assert.False(body.GetProperty("available").GetBoolean());
        Assert.False(body.GetProperty("openNow").GetBoolean());
        Assert.Equal("", body.GetProperty("hotline").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("openingHours").ValueKind);
        Assert.Empty(body.GetProperty("menu").EnumerateArray());
    }

    [Fact]
    public async Task A_station_without_catering_is_200_unavailable_and_an_unknown_station_is_404()
    {
        var fort = await Catering("FOT");
        Assert.False(fort.GetProperty("available").GetBoolean());

        var unknown = await _client.GetAsync("/api/v1/stations/00000000-0000-0000-0000-000000000001/catering", Ct);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("NOT_FOUND", (await unknown.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/v1/stations/not-a-guid/catering", Ct)).StatusCode);
    }
}
