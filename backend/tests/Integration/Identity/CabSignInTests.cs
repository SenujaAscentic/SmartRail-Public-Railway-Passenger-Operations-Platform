using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartRail.Identity.Persistence;
using Xunit;

namespace SmartRail.IntegrationTests.Identity;

// POST /api/v1/auth/crew/cab-sign-in   (contract 3.4, US-C2, ADR-05, R-09, R-19)
// The tablet proves itself with a device credential in headers, and the driver with staff number and password.
[Collection("Api")]
public class CabSignInTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _client = api.Factory.NewClient();

    private Task<HttpResponseMessage> CabAsync(
        string device = "CAB-01", string? secret = null, string staff = "DRV-01", string? password = null, DateTimeOffset? deviceTime = null) =>
        _client.CabSignInAsync(device, secret ?? api.CabDeviceSecrets.GetValueOrDefault(device, "unknown-device-secret"),
            staff, password ?? api.DemoPassword, deviceTime ?? api.Clock.GetUtcNow(), Ct);

    private async Task<T> InDb<T>(Func<IdentityModuleDbContext, Task<T>> query)
    {
        using var scope = api.Factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>());
    }

    private Task<int> ActiveSessionsOf(string staffNumber) =>
        InDb(db => db.CabSessions.CountAsync(s => s.EndedAt == null && db.Users.Any(u => u.Id == s.AccountId && u.UserName == staffNumber), Ct));

    private async Task AssertGenericRefusal(HttpResponseMessage response)
    {
        var baseline = await (await _client.CrewSignInAsync("NOBODY-00", "not-the-password", Ct)).ReadJsonAsync(Ct);   // an unknown number: the generic answer, and nobody's lock count moves
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("UNAUTHENTICATED", await response.ProblemCodeAsync(Ct));
        var body = await response.ReadJsonAsync(Ct);
        Assert.Equal(baseline.GetProperty("title").GetString(), body.GetProperty("title").GetString());
        Assert.Equal(baseline.GetProperty("detail").GetString(), body.GetProperty("detail").GetString());
    }

    // ---- success ----

    [Fact]
    public async Task A_driver_on_a_registered_tablet_gets_tokens_a_session_and_the_server_time()
    {
        var response = await CabAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync(Ct);
        Assert.Equal(api.Clock.GetUtcNow().AddMinutes(15), body.GetProperty("expiresAt").GetDateTimeOffset());
        Assert.Equal(api.Clock.GetUtcNow().AddHours(12), body.GetProperty("refreshExpiresAt").GetDateTimeOffset());   // a shift, fixed
        Assert.False(string.IsNullOrEmpty(body.GetProperty("refreshToken").GetString()));
        Assert.NotEqual(Guid.Empty, body.GetProperty("crewSessionId").GetGuid());
        Assert.Equal(api.Clock.GetUtcNow(), body.GetProperty("serverTime").GetDateTimeOffset());
        Assert.Null(response.RefreshSetCookie());
    }

    [Fact]
    public async Task The_token_carries_the_role_the_device_the_session_and_the_measured_clock_offset()
    {
        var deviceId = await InDb(db => db.CabDevices.Where(d => d.Code == "CAB-01").Select(d => d.Id).SingleAsync(Ct));

        var response = await CabAsync(deviceTime: api.Clock.GetUtcNow().AddSeconds(-5));   // the tablet runs 5 s slow

        var body = await response.ReadJsonAsync(Ct);
        var jwt = AuthTestHelpers.Read(body.GetProperty("accessToken").GetString()!);
        Assert.Equal("Driver", jwt.GetClaim("role").Value);
        Assert.Equal(deviceId.ToString(), jwt.GetClaim("deviceId").Value);
        Assert.Equal(body.GetProperty("crewSessionId").GetGuid().ToString(), jwt.GetClaim("crewSessionId").Value);
        Assert.Equal("5000", jwt.GetClaim("clockOffsetMs").Value);
    }

    [Fact]
    public async Task The_session_is_stored_with_the_driver_the_tablet_and_the_offset()
    {
        var body = await (await CabAsync(device: "CAB-02", staff: "DRV-02", deviceTime: api.Clock.GetUtcNow().AddMinutes(3))).ReadJsonAsync(Ct);

        var session = await InDb(db => db.CabSessions.AsNoTracking().SingleAsync(s => s.Id == body.GetProperty("crewSessionId").GetGuid(), Ct));
        Assert.Null(session.EndedAt);
        Assert.Equal(api.Clock.GetUtcNow(), session.StartedAt);
        Assert.Equal(-180_000, session.ClockOffsetMs);
        Assert.Equal(1, await ActiveSessionsOf("DRV-02"));
    }

    // ---- every credential problem is the same generic 401 (contract section 6, threat T-12) ----

    [Fact]
    public async Task A_wrong_device_secret_is_the_generic_401()
    {
        var response = await CabAsync(secret: "not-the-device-secret");

        await AssertGenericRefusal(response);
    }

    [Fact]
    public async Task An_unknown_device_is_the_generic_401()
    {
        var response = await CabAsync(device: "CAB-99");

        await AssertGenericRefusal(response);
    }

    [Fact]
    public async Task A_deactivated_device_is_the_generic_401_even_with_the_right_secret()
    {
        await SetDeviceActive("CAB-04", false);
        try
        {
            var response = await CabAsync(device: "CAB-04", staff: "DRV-04");

            await AssertGenericRefusal(response);
            Assert.Equal(0, await ActiveSessionsOf("DRV-04"));
        }
        finally
        {
            await SetDeviceActive("CAB-04", true);
        }
    }

    [Fact]
    public async Task A_wrong_driver_password_and_an_unknown_staff_number_are_the_generic_401()
    {
        await AssertGenericRefusal(await CabAsync(password: "not-the-password"));
        await AssertGenericRefusal(await CabAsync(staff: "NOBODY-99"));
    }

    [Theory]
    [InlineData("CTRL-01")]
    [InlineData("SM-FOT")]
    public async Task Staff_accounts_cannot_sign_in_on_a_tablet(string staffNumber)
    {
        var response = await CabAsync(staff: staffNumber);

        await AssertGenericRefusal(response);
        Assert.Equal(0, await ActiveSessionsOf(staffNumber));
    }

    [Fact]
    public async Task A_bad_device_credential_never_counts_against_the_drivers_lockout()
    {
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await CabAsync(staff: "DRV-02", secret: "guess-" + attempt)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await CabAsync(device: "CAB-02", staff: "DRV-02")).StatusCode);   // not locked
    }

    [Fact]
    public async Task Five_wrong_driver_passwords_on_a_valid_tablet_lock_the_account()
    {
        try
        {
            for (var attempt = 1; attempt <= 4; attempt++)
            {
                Assert.Equal(HttpStatusCode.Unauthorized, (await CabAsync(device: "CAB-03", staff: "DRV-03", password: "wrong-" + attempt)).StatusCode);
            }

            var fifth = await CabAsync(device: "CAB-03", staff: "DRV-03", password: "wrong-5");

            Assert.Equal(HttpStatusCode.TooManyRequests, fifth.StatusCode);
            Assert.Equal("SIGN_IN_LOCKED", await fifth.ProblemCodeAsync(Ct));
        }
        finally
        {
            await InDb(async db =>
            {
                var account = await db.Users.SingleAsync(a => a.UserName == "DRV-03", Ct);
                account.LockoutEnd = null;
                account.AccessFailedCount = 0;
                return await db.SaveChangesAsync(Ct);
            });
        }
    }

    // ---- request problems are 400 ----

    [Theory]
    [InlineData(null, "x", "X-Device-Id")]
    [InlineData("CAB-01", null, "X-Device-Secret")]
    public async Task A_missing_device_header_gets_a_400_naming_it(string? deviceId, string? deviceSecret, string header)
    {
        var response = await _client.CabSignInAsync(deviceId, deviceSecret, "DRV-01", api.DemoPassword, api.Clock.GetUtcNow(), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await response.ProblemCodeAsync(Ct));
        Assert.True((await response.ReadJsonAsync(Ct)).GetProperty("errors").TryGetProperty(header, out _));
    }

    [Fact]
    public async Task A_missing_device_time_gets_a_400_naming_it()
    {
        var response = await _client.CabSignInAsync("CAB-01", api.CabDeviceSecrets["CAB-01"], "DRV-01", api.DemoPassword, deviceTime: null, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadJsonAsync(Ct)).GetProperty("errors").TryGetProperty("deviceTime", out _));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(-25)]
    public async Task A_tablet_clock_more_than_24_hours_off_is_refused_with_a_400_and_starts_no_session(int hoursOff)
    {
        var before = await ActiveSessionsOf("DRV-04");

        var response = await CabAsync(device: "CAB-04", staff: "DRV-04", deviceTime: api.Clock.GetUtcNow().AddHours(hoursOff));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadJsonAsync(Ct)).GetProperty("errors").TryGetProperty("deviceTime", out _));
        Assert.Equal(before, await ActiveSessionsOf("DRV-04"));
    }

    // ---- R-19: one active cab session per driver, and one per tablet ----

    [Fact]
    public async Task Signing_in_on_another_tablet_ends_the_drivers_first_session_completely()
    {
        var first = await (await CabAsync(device: "CAB-02", staff: "DRV-02")).ReadJsonAsync(Ct);
        var firstAccess = first.GetProperty("accessToken").GetString()!;
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/v1/staff/me", firstAccess, Ct)).StatusCode);   // accepted: wrong role only

        var second = await (await CabAsync(device: "CAB-03", staff: "DRV-02")).ReadJsonAsync(Ct);

        Assert.Equal(1, await ActiveSessionsOf("DRV-02"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/staff/me", firstAccess, Ct)).StatusCode);   // refused at once
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.RefreshWithBodyAsync(first.GetProperty("refreshToken").GetString(), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/v1/staff/me", second.GetProperty("accessToken").GetString()!, Ct)).StatusCode);
    }

    [Fact]
    public async Task A_second_driver_signing_in_on_the_same_tablet_ends_the_first_drivers_session()
    {
        var first = await (await CabAsync(device: "CAB-02", staff: "DRV-02")).ReadJsonAsync(Ct);

        await CabAsync(device: "CAB-02", staff: "DRV-04");

        Assert.Equal(0, await ActiveSessionsOf("DRV-02"));
        Assert.Equal(1, await ActiveSessionsOf("DRV-04"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/staff/me", first.GetProperty("accessToken").GetString()!, Ct)).StatusCode);
    }

    [Fact]
    public async Task Two_sign_ins_of_one_driver_at_the_same_moment_leave_exactly_one_active_session()
    {
        var results = await Task.WhenAll(CabAsync(device: "CAB-02", staff: "DRV-02"), CabAsync(device: "CAB-03", staff: "DRV-02"));

        Assert.All(results, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));   // never a 500
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(1, await ActiveSessionsOf("DRV-02"));
    }

    // ---- secrets stay out of the logs (answer 5, 15 Oct) ----

    [Fact]
    public async Task Neither_the_device_secret_nor_the_password_is_ever_written_to_a_log()
    {
        var log = new LogCapture("", LogLevel.Trace);   // every category, every level
        using var factory = api.CreateFactory(builder => builder.ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddProvider(log);
        }));
        var client = factory.NewClient();
        var secret = api.CabDeviceSecrets["CAB-01"];

        await client.CabSignInAsync("CAB-01", secret, "DRV-01", api.DemoPassword, api.Clock.GetUtcNow(), Ct);                 // success
        await client.CabSignInAsync("CAB-01", "a-wrong-device-secret-1234", "DRV-01", api.DemoPassword, api.Clock.GetUtcNow(), Ct);   // refused
        await client.CabSignInAsync("CAB-01", secret, "DRV-01", "a-wrong-password-1234", api.Clock.GetUtcNow(), Ct);          // refused

        Assert.NotEmpty(log.Messages);   // the capture works: something was logged
        foreach (var forbidden in new[] { secret, "a-wrong-device-secret-1234", api.DemoPassword, "a-wrong-password-1234" })
        {
            Assert.DoesNotContain(log.Messages, m => m.Message.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    private Task SetDeviceActive(string code, bool active) =>
        InDb(async db =>
        {
            var device = await db.CabDevices.SingleAsync(d => d.Code == code, Ct);
            device.Active = active;
            return await db.SaveChangesAsync(Ct);
        });
}
