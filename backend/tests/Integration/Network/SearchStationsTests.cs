using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.Network.Model;
using SmartRail.Network.Persistence;
using Xunit;

namespace SmartRail.IntegrationTests.Network;

// GET /api/v1/stations?query=   (contract 3.1)
[Collection("Api")]
public class SearchStationsTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _client = api.Factory.CreateClient();

    private async Task<List<string>> NamesFor(string query)
    {
        var response = await _client.GetAsync("/api/v1/stations?query=" + Uri.EscapeDataString(query), Ct);
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return rows.EnumerateArray().Select(r => r.GetProperty("name").GetString()!).ToList();
    }

    [Fact]
    public async Task Typing_kan_lists_Kandy_first_then_Rambukkana_which_contains_it()
    {
        Assert.Equal(["Kandy", "Rambukkana"], await NamesFor("kan"));
    }

    [Fact]
    public async Task Search_ignores_upper_and_lower_case()
    {
        Assert.Equal(["Kandy", "Rambukkana"], await NamesFor("KAN"));
    }

    [Fact]
    public async Task Search_also_matches_the_station_code()
    {
        Assert.Equal(["Kandy"], await NamesFor("kdt"));
    }

    [Fact]
    public async Task Names_that_start_with_the_text_come_first_then_the_others()
    {
        // "ka" starts Kadugannawa and Kandy; Rambukkana only contains it.
        Assert.Equal(["Kadugannawa", "Kandy", "Rambukkana"], await NamesFor("ka"));
    }

    [Fact]
    public async Task A_search_with_no_match_returns_an_empty_list()
    {
        Assert.Empty(await NamesFor("nowhere"));
    }

    [Fact]
    public async Task A_percent_sign_is_searched_literally_not_as_a_wildcard()
    {
        Assert.Empty(await NamesFor("%"));
    }

    [Fact]
    public async Task Each_row_has_only_id_code_and_name()
    {
        var response = await _client.GetAsync("/api/v1/stations?query=kandy", Ct);
        var rows = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

        var row = Assert.Single(rows.EnumerateArray());
        Assert.Equal(["id", "code", "name"], row.EnumerateObject().Select(p => p.Name));
        Assert.True(Guid.TryParse(row.GetProperty("id").GetString(), out _));
        Assert.Equal("KDT", row.GetProperty("code").GetString());
    }

    [Fact]
    public async Task At_most_10_stations_are_returned()
    {
        // The seed has only 11 stations, so add 12 extra ones for this test and remove them afterwards.
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NetworkDbContext>();
        var extra = Enumerable.Range(1, 12)
            .Select(n => new Station { Id = Guid.CreateVersion7(), Code = $"ZZ{n:00}", Name = $"Zzz Station {n:00}" })
            .ToList();
        db.Stations.AddRange(extra);
        await db.SaveChangesAsync(Ct);

        try
        {
            Assert.Equal(10, (await NamesFor("zzz")).Count);
        }
        finally
        {
            db.Stations.RemoveRange(extra);
            await db.SaveChangesAsync(Ct);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_query_is_refused_with_VALIDATION_FAILED(string query)
    {
        var response = await _client.GetAsync("/api/v1/stations?query=" + Uri.EscapeDataString(query), Ct);

        await AssertValidationFailed(response);
    }

    [Fact]
    public async Task A_missing_query_is_refused_with_VALIDATION_FAILED()
    {
        var response = await _client.GetAsync("/api/v1/stations", Ct);

        await AssertValidationFailed(response);
    }

    [Fact]
    public async Task A_query_longer_than_50_characters_is_refused()
    {
        var response = await _client.GetAsync("/api/v1/stations?query=" + new string('a', 51), Ct);

        await AssertValidationFailed(response);
    }

    // Contract section 6: Problem Details with a stable code, field errors and a request id.
    private static async Task AssertValidationFailed(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("VALIDATION_FAILED", problem.GetProperty("code").GetString());
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.NotEmpty(problem.GetProperty("errors").GetProperty("query").EnumerateArray());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("requestId").GetString()));
    }
}
