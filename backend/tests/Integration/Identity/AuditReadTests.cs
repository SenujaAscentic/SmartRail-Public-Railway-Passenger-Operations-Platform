using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmartRail.IntegrationTests.LiveOperations;
using Xunit;

namespace SmartRail.IntegrationTests.Identity;

// GET /ops/audit (policy Controller, US-O10, domain model O-10): the trail written by the controller's decisions, read back.
[Collection("Api")]
public class AuditReadTests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateOnly Day = new(2027, 3, 11);

    private readonly HttpClient _client = api.Factory.NewClient();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    private async Task<HttpResponseMessage> Get(string query, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ops/audit" + query);
        request.Headers.Add("Authorization", "Bearer " + (token ?? await api.TokenForAsync("Controller", Ct)));
        return await _client.SendAsync(request, Ct);
    }

    private async Task DelayAndClear(Guid journeyId)
    {
        var token = await api.TokenForAsync("Controller", Ct);
        async Task Send(HttpMethod method, object body)
        {
            var request = new HttpRequestMessage(method, $"/api/v1/ops/journeys/{journeyId}/delay") { Content = JsonContent.Create(body) };
            request.Headers.Add("Authorization", "Bearer " + token);
            Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(request, Ct)).StatusCode);
        }

        await Send(HttpMethod.Post, new { minutes = 6, reasonCode = "Weather", note = "Heavy rain", expectedVersion = 0 });
        await Send(HttpMethod.Delete, new { expectedVersion = 1 });
    }

    [Fact]
    public async Task The_trail_of_a_journey_shows_each_decision_newest_first_with_who_did_it()
    {
        var journeyId = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Day);
        await DelayAndClear(journeyId);

        var entries = await (await Get($"?journeyId={journeyId}")).Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(["DelayCleared", "DelaySet"], entries.EnumerateArray().Select(e => e.GetProperty("action").GetString()));
        var set = entries[1];
        Assert.Equal("Journey", set.GetProperty("targetType").GetString());
        Assert.Equal(journeyId, set.GetProperty("targetId").GetGuid());
        Assert.Equal("Weather", set.GetProperty("reason").GetString());
        Assert.Equal("Delay set to 6 min", set.GetProperty("summary").GetString());
        Assert.Equal("CTRL-01", set.GetProperty("actor").GetProperty("staffNumber").GetString());
        Assert.Equal("Controller", set.GetProperty("actor").GetProperty("role").GetString());
        Assert.DoesNotContain("Heavy rain", set.GetRawText());   // the note is not audited
    }

    [Fact]
    public async Task The_trail_can_be_filtered_by_staff_number_and_by_time_and_limited()
    {
        var journeyId = await RealJourneys.CreateAsync(api.Factory.Services, "1004", Day);
        await DelayAndClear(journeyId);
        var now = api.Clock.GetUtcNow().UtcDateTime;

        var mine = await (await Get($"?journeyId={journeyId}&staff=CTRL-01")).Content.ReadFromJsonAsync<JsonElement>(Ct);
        var someoneElse = await (await Get($"?journeyId={journeyId}&staff=SM-KDT")).Content.ReadFromJsonAsync<JsonElement>(Ct);
        var nobody = await (await Get($"?journeyId={journeyId}&staff=NOBODY-9")).Content.ReadFromJsonAsync<JsonElement>(Ct);
        var inRange = await (await Get($"?journeyId={journeyId}&from={now.AddMinutes(-1):O}&to={now.AddMinutes(1):O}")).Content.ReadFromJsonAsync<JsonElement>(Ct);
        var tooEarly = await (await Get($"?journeyId={journeyId}&to={now.AddHours(-1):O}")).Content.ReadFromJsonAsync<JsonElement>(Ct);
        var one = await (await Get($"?journeyId={journeyId}&limit=1")).Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(2, mine.GetArrayLength());
        Assert.Equal(0, someoneElse.GetArrayLength());
        Assert.Equal(0, nobody.GetArrayLength());
        Assert.Equal(2, inRange.GetArrayLength());
        Assert.Equal(0, tooEarly.GetArrayLength());
        Assert.Equal(1, one.GetArrayLength());
    }

    [Fact]
    public async Task Bad_filters_are_a_400_and_only_the_controller_may_read()
    {
        var bad = await Get("?journeyId=nope&from=yesterday&limit=0");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var errors = (await bad.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("journeyId", out _) && errors.TryGetProperty("from", out _) && errors.TryGetProperty("limit", out _));

        Assert.Equal(HttpStatusCode.Forbidden, (await Get("", await api.TokenForAsync("StationManager", Ct))).StatusCode);
    }

    [Fact]
    public async Task The_trail_cannot_be_changed_through_the_api()
    {
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete, HttpMethod.Patch })
        {
            var request = new HttpRequestMessage(method, "/api/v1/ops/audit") { Content = JsonContent.Create(new { }) };
            request.Headers.Add("Authorization", "Bearer " + await api.TokenForAsync("Controller", Ct));

            var status = (await _client.SendAsync(request, Ct)).StatusCode;

            Assert.True(status is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound, $"{method} answered {status}");
        }
    }
}
