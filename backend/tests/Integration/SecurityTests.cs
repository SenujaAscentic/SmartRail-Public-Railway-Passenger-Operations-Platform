using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.Identity;
using SmartRail.IntegrationTests.Identity;
using SmartRail.IntegrationTests.LiveOperations;
using SmartRail.LiveOperations.Domain;
using SmartRail.LiveOperations.Persistence;
using Xunit;

namespace SmartRail.IntegrationTests;

// Security tests from the threat model (section 6): T-19 over-posting, T-08 / T-09 injection strings, T-06 personal data and secrets
// in the log. The test clock is 14 Oct 2026, 04:30 UTC.
[Collection("Api")]
public class SecurityTests(ApiFixture api) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _client = api.Factory.NewClient();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await JourneyCleanup.RemoveAllAsync(api.ConnectionString);

    // ---- T-19: over-posting ----

    // The server sets `source` from the credential. A request that names another one (in the batch, in a position, or as the delay's own
    // source) is not refused and not obeyed: the stored sources are the ones the server derives (GPS for a position).
    [Fact]
    public async Task A_source_field_in_a_crew_request_body_is_ignored_and_the_server_sets_the_source_from_the_credential()
    {
        Guid driver;
        using (var scope = api.Factory.Services.CreateScope())
        {
            driver = (await scope.ServiceProvider.GetRequiredService<IStaffDirectory>().GetDriverIdsAsync(["DRV-02"], Ct))["DRV-02"];
        }

        var day = new DateOnly(2027, 5, 5);
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1004", day, j =>
        {
            j.AssignDriver(driver);
            j.Start(j.Stops[0].ScheduledDeparture!.Value, Guid.NewGuid(), Guid.NewGuid());
        });
        var token = await (await _client.CabSignInAsync("CAB-02", api.CabDeviceSecrets["CAB-02"], "DRV-02", api.DemoPassword, api.Clock.GetUtcNow(), Ct)).AccessTokenAsync(Ct);

        var kandy = await Coordinates("KDT");
        var now = api.Clock.GetUtcNow();
        object Fix(DateTimeOffset at, double speed) => new
        {
            lat = kandy.Lat, lng = kandy.Lng, speedKmh = speed, accuracyM = 5.0, gpsTime = at, deviceTime = at,
            source = "Controller", delaySource = "Controller",   // neither exists in the contract
        };
        var body = new
        {
            clientActionId = Guid.NewGuid(), source = "Controller", delaySource = "Controller",
            positions = new[] { Fix(now, 3), Fix(now.AddSeconds(25), 0) },      // slow inside Kandy's zone for 20 s: Arrived
        };
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/crew/journeys/{id}/positions") { Content = JsonContent.Create(body) };
        request.Headers.Add("Authorization", "Bearer " + token);

        var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);   // extra fields are not an error ...
        using var check = api.Factory.Services.CreateScope();
        var journey = await check.ServiceProvider.GetRequiredService<LiveOperationsDbContext>().Journeys.AsNoTracking().Include(j => j.Stops).SingleAsync(j => j.Id == id, Ct);
        var origin = journey.Stops.OrderBy(s => s.Sequence).First();
        Assert.Equal(StopStatus.Arrived, origin.Status);
        Assert.Equal(Source.Gps, origin.ActualArrivalSource);   // ... and not obeyed: the position is GPS, not Controller
        Assert.NotEqual(Source.Controller, journey.DelaySource);
        Assert.Equal(0, journey.ControlVersion);                  // and nothing counts as a controller decision (R-18)
    }

    private async Task<(double Lat, double Lng)> Coordinates(string code)
    {
        var id = (await RealJourneys.StationsAsync(api.Factory.Services, code))[code];
        var station = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/stations/{id}", Ct);
        return (station.GetProperty("latitude").GetDouble(), station.GetProperty("longitude").GetDouble());
    }

    // ---- T-08 / T-09: injection strings ----

    public static TheoryData<string> Strings => new()
    {
        "<script>alert('x')</script>",
        "\"><img src=x onerror=alert(1)>",
        "'; DROP TABLE ops.\"Journeys\"; --",
        "Robert'); DELETE FROM identity.\"StaffAccounts\";--",
        "{{7*7}} ${jndi:ldap://example.invalid/a} %s %n",
        "ශ්‍රී ලංකා 🚆 ‮ right-to-left",
    };

    [Theory]
    [MemberData(nameof(Strings))]
    public async Task A_note_with_markup_or_SQL_is_stored_and_returned_as_the_same_plain_text_without_an_error(string note)
    {
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1004", new DateOnly(2027, 5, 6));
        var token = await api.TokenForAsync("Controller", Ct);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ops/journeys/{id}/delay")
        {
            Content = JsonContent.Create(new { minutes = 7, reasonCode = "Weather", note, expectedVersion = 0 }),
        };
        request.Headers.Add("Authorization", "Bearer " + token);

        var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType!.MediaType);   // data, never HTML
        var detail = await _client.GetAsync($"/api/v1/journeys/{id}", Ct);
        Assert.StartsWith("application/json", detail.Content.Headers.ContentType!.MediaType);
        var json = await detail.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(note, json.GetProperty("reason").GetProperty("note").GetString());   // exactly what was sent, as text
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/v1/lines", Ct)).StatusCode);   // the database is intact
    }

    [Fact]
    public async Task A_reason_code_with_SQL_in_it_is_a_clean_400_not_a_server_error()
    {
        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1004", new DateOnly(2027, 5, 7));
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ops/journeys/{id}/delay")
        {
            Content = JsonContent.Create(new { minutes = 7, reasonCode = "Weather'; DROP TABLE ops.\"Journeys\"; --", note = "x", expectedVersion = 0 }),
        };
        request.Headers.Add("Authorization", "Bearer " + await api.TokenForAsync("Controller", Ct));

        var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }

    [Theory]
    [MemberData(nameof(Strings))]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    [InlineData("\" OR \"\"=\"")]
    public async Task A_station_search_with_injection_strings_is_an_answer_not_an_error_and_search_still_works_afterwards(string query)
    {
        var response = await _client.GetAsync("/api/v1/stations?query=" + Uri.EscapeDataString(query), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Array, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).ValueKind);
        var after = await _client.GetFromJsonAsync<JsonElement>("/api/v1/stations?query=kan", Ct);
        Assert.Equal("Kandy", after[0].GetProperty("name").GetString());
    }

    // ---- T-06: no personal data, secrets or tokens in any log line ----

    // The scenario signs in (staff, driver phone, cab tablet), refreshes, decides with a note that looks like personal data, and posts a
    // position. Then EVERY line the test applications have logged so far in this run (all tests of the "Api" collection share the log
    // capture) is searched: no secret or token this run used, no e-mail address and no phone number. The scan covers the lines written
    // by the tests that ran before this one as well.
    [Fact]
    public async Task No_log_line_of_the_run_contains_a_password_a_secret_a_token_an_email_address_or_a_phone_number()
    {
        var personal = "Call Nimal on 0771234567 / +94 77 123 4567 or nimal.perera@example.com";
        var secrets = new List<string> { api.DemoPassword, api.JwtSigningKey };
        secrets.AddRange(api.CabDeviceSecrets.Values);

        var staff = await _client.SignInAsync("CTRL-01", api.DemoPassword, Ct);
        var controllerToken = await staff.AccessTokenAsync(Ct);
        var phone = await _client.CrewSignInAsync("DRV-01", api.DemoPassword, Ct);
        var cab = await _client.CabSignInAsync("CAB-01", api.CabDeviceSecrets["CAB-01"], "DRV-01", api.DemoPassword, api.Clock.GetUtcNow(), Ct);
        var tokens = new List<string> { controllerToken, await phone.AccessTokenAsync(Ct), await cab.AccessTokenAsync(Ct) };
        var cabBody = await cab.Content.ReadFromJsonAsync<JsonElement>(Ct);
        tokens.Add(cabBody.GetProperty("refreshToken").GetString()!);
        var cookie = staff.RefreshCookieValue();
        if (cookie is not null)
        {
            tokens.Add(cookie);
        }

        // A wrong password and a wrong tablet secret are logged (or not) too: they must not appear either.
        await _client.SignInAsync("CTRL-01", "Wrong-Password-" + Guid.NewGuid().ToString("N"), Ct);
        secrets.Add("Wrong-Device-Secret-" + Guid.NewGuid().ToString("N"));
        await _client.CabSignInAsync("CAB-01", secrets[^1], "DRV-01", api.DemoPassword, api.Clock.GetUtcNow(), Ct);

        var id = await RealJourneys.CreateAsync(api.Factory.Services, "1004", new DateOnly(2027, 5, 8));
        var decide = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ops/journeys/{id}/delay")
        {
            Content = JsonContent.Create(new { minutes = 9, reasonCode = "Weather", note = personal, expectedVersion = 0 }),
        };
        decide.Headers.Add("Authorization", "Bearer " + controllerToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(decide, Ct)).StatusCode);
        await _client.GetAsync($"/api/v1/journeys/{id}", Ct);
        await Task.Delay(1500, Ct);   // the outbox dispatcher logs what it published

        var lines = api.AllLogs.Messages.Select(m => m.Message).ToList();
        Assert.True(lines.Count > 5, $"Only {lines.Count} log lines were captured: the scan would prove nothing.");

        var email = new Regex(@"[\w.+-]+@[\w-]+\.[\w.-]+");
        var phoneNumber = new Regex(@"\+\d[\d ]{8,}\d|\b0\d{9}\b|\b\d{3}[ -]\d{3}[ -]\d{4}\b");
        var found = new List<string>();
        foreach (var line in lines)
        {
            foreach (var secret in secrets.Concat(tokens).Concat([personal, "nimal", "0771234567"]))
            {
                if (line.Contains(secret, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add($"contains a secret, token or the note: {line[..Math.Min(line.Length, 160)]}");
                }
            }

            if (email.IsMatch(line) || phoneNumber.IsMatch(line))
            {
                found.Add($"looks like an e-mail address or a phone number: {line[..Math.Min(line.Length, 160)]}");
            }
        }

        Assert.True(found.Count == 0, string.Join(Environment.NewLine, found.Distinct().Take(10)));
    }
}
