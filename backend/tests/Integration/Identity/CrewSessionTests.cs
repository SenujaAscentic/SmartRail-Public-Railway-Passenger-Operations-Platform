using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SmartRail.Identity.Model;
using SmartRail.Identity.Persistence;
using SmartRail.Identity.Tokens;
using Xunit;

namespace SmartRail.IntegrationTests.Identity;

// What happens AFTER a crew sign-in: refreshing with the token in the body, the 12-hour shift, the check of the cab
// session on every request, and signing out. (Staff keep the cookie; see RefreshSessionTests.)
[Collection("Api")]
public class CrewSessionTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string MePath = "/api/v1/staff/me";   // a Staff endpoint: a driver is "authenticated but not allowed" (403) or "not authenticated" (401)

    private readonly HttpClient _client = api.Factory.NewClient();

    private (WebApplicationFactory<Program> Factory, FakeTimeProvider Clock) StartWithOwnClock()
    {
        var clock = new FakeTimeProvider(api.Clock.GetUtcNow());
        var factory = api.CreateFactory(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        return (factory, clock);
    }

    private Task<HttpResponseMessage> CabSignIn(HttpClient client, string device, string staff, long secondsSlow = 0) =>
        client.CabSignInAsync(device, api.CabDeviceSecrets[device], staff, api.DemoPassword, api.Clock.GetUtcNow().AddSeconds(-secondsSlow), Ct);

    private async Task<T> InDb<T>(Func<IdentityModuleDbContext, Task<T>> query)
    {
        using var scope = api.Factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>());
    }

    // ---- refresh with the token in the body ----

    [Fact]
    public async Task A_phone_refresh_with_the_body_token_gives_new_tokens_and_no_cookie()
    {
        var signIn = await _client.CrewSignInAsync("DRV-01", api.DemoPassword, Ct);
        var first = (await signIn.ReadJsonAsync(Ct));

        var response = await _client.RefreshWithBodyAsync(first.GetProperty("refreshToken").GetString(), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync(Ct);
        Assert.NotEqual(first.GetProperty("refreshToken").GetString(), body.GetProperty("refreshToken").GetString());
        Assert.Equal(first.GetProperty("refreshExpiresAt").GetDateTimeOffset(), body.GetProperty("refreshExpiresAt").GetDateTimeOffset());   // the shift end does not move
        Assert.Null(response.RefreshSetCookie());
        Assert.Equal("Driver", AuthTestHelpers.Read(body.GetProperty("accessToken").GetString()!).GetClaim("role").Value);
    }

    [Fact]
    public async Task The_shift_ends_12_hours_after_sign_in_however_often_the_app_refreshed()
    {
        var (factory, clock) = StartWithOwnClock();
        using var _ = factory;
        var client = factory.NewClient();
        var token = await (await client.CrewSignInAsync("DRV-01", api.DemoPassword, Ct)).RefreshTokenAsync(Ct);

        clock.Advance(TimeSpan.FromHours(6));
        var secondResponse = await client.RefreshWithBodyAsync(token, Ct);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        clock.Advance(TimeSpan.FromHours(5) + TimeSpan.FromMinutes(59));   // 11 h 59 min after sign-in
        var thirdResponse = await client.RefreshWithBodyAsync(await secondResponse.RefreshTokenAsync(Ct), Ct);
        Assert.Equal(HttpStatusCode.OK, thirdResponse.StatusCode);

        clock.Advance(TimeSpan.FromMinutes(2));                             // 12 h 01 min
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.RefreshWithBodyAsync(await thirdResponse.RefreshTokenAsync(Ct), Ct)).StatusCode);
    }

    [Fact]
    public async Task A_staff_refresh_token_in_the_body_is_refused_because_staff_use_the_cookie_only()
    {
        var staff = await _client.SignInAsync("CTRL-01", api.DemoPassword, Ct);

        var response = await _client.RefreshWithBodyAsync(staff.RefreshCookieValue(), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_crew_refresh_token_in_the_cookie_is_refused_because_crew_use_the_body_only()
    {
        var crewToken = await (await _client.CrewSignInAsync("DRV-01", api.DemoPassword, Ct)).RefreshTokenAsync(Ct);

        var response = await _client.RefreshAsync(crewToken, Ct);   // sent as the cookie

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_crew_refresh_token_used_again_after_10_seconds_ends_the_session()
    {
        var (factory, clock) = StartWithOwnClock();
        using var _ = factory;
        var client = factory.NewClient();
        var first = await (await client.CrewSignInAsync("DRV-01", api.DemoPassword, Ct)).RefreshTokenAsync(Ct);
        var refreshed = await client.RefreshWithBodyAsync(first, Ct);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        clock.Advance(TimeSpan.FromSeconds(11));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.RefreshWithBodyAsync(first, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.RefreshWithBodyAsync(await refreshed.RefreshTokenAsync(Ct), Ct)).StatusCode);
    }

    [Fact]
    public async Task A_refresh_with_no_token_or_an_unknown_one_is_refused()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.RefreshWithBodyAsync(null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.RefreshWithBodyAsync("this-was-never-issued", Ct)).StatusCode);
    }

    // ---- a cab session survives a refresh, and ends when the device or the driver is switched off ----

    [Fact]
    public async Task A_cab_refresh_keeps_the_same_session_the_same_device_and_the_same_clock_offset()
    {
        var signIn = await (await CabSignIn(_client, "CAB-01", "DRV-01", secondsSlow: 5)).ReadJsonAsync(Ct);

        var refreshed = await _client.RefreshWithBodyAsync(signIn.GetProperty("refreshToken").GetString(), Ct);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var before = AuthTestHelpers.Read(signIn.GetProperty("accessToken").GetString()!);
        var after = AuthTestHelpers.Read(await refreshed.AccessTokenAsync(Ct));
        Assert.Equal(before.GetClaim("crewSessionId").Value, after.GetClaim("crewSessionId").Value);
        Assert.Equal(before.GetClaim("deviceId").Value, after.GetClaim("deviceId").Value);
        Assert.Equal("5000", after.GetClaim("clockOffsetMs").Value);
    }

    [Fact]
    public async Task A_deactivated_device_is_refused_at_its_next_refresh_and_the_session_ends()
    {
        var signIn = await (await CabSignIn(_client, "CAB-04", "DRV-04")).ReadJsonAsync(Ct);
        await SetDeviceActive("CAB-04", false);
        try
        {
            var response = await _client.RefreshWithBodyAsync(signIn.GetProperty("refreshToken").GetString(), Ct);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var session = await InDb(db => db.CabSessions.AsNoTracking().SingleAsync(s => s.Id == signIn.GetProperty("crewSessionId").GetGuid(), Ct));
            Assert.NotNull(session.EndedAt);
        }
        finally
        {
            await SetDeviceActive("CAB-04", true);
        }
    }

    [Fact]
    public async Task A_deactivated_driver_is_refused_at_the_next_refresh()
    {
        var token = await (await _client.CrewSignInAsync("DRV-02", api.DemoPassword, Ct)).RefreshTokenAsync(Ct);
        await SetDriverActive("DRV-02", false);
        try
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.RefreshWithBodyAsync(token, Ct)).StatusCode);
        }
        finally
        {
            await SetDriverActive("DRV-02", true);
        }
    }

    // ---- the cab session is checked on every request that carries crewSessionId ----

    [Fact]
    public async Task A_cab_token_is_accepted_while_its_session_is_active()
    {
        var signIn = await (await CabSignIn(_client, "CAB-01", "DRV-01")).ReadJsonAsync(Ct);

        var response = await _client.GetAsync(MePath, signIn.GetProperty("accessToken").GetString(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);   // known and valid, but a driver is not Staff
    }

    [Fact]
    public async Task A_cab_token_for_a_session_that_does_not_exist_is_refused_even_though_it_is_properly_signed()
    {
        var token = api.Factory.Services.GetRequiredService<AccessTokenIssuer>()
            .Issue(Guid.NewGuid(), StaffRole.Driver, null, new CabSessionClaims(Guid.NewGuid(), Guid.NewGuid(), 0)).Token;

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(MePath, token, Ct)).StatusCode);
    }

    [Fact]
    public async Task A_phone_token_does_not_depend_on_any_cab_session()
    {
        var phone = await (await _client.CrewSignInAsync("DRV-04", api.DemoPassword, Ct)).AccessTokenAsync(Ct);

        await CabSignIn(_client, "CAB-04", "DRV-04");   // the same driver starts a cab session
        await CabSignIn(_client, "CAB-04", "DRV-04");   // and starts another, which ends the first

        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync(MePath, phone, Ct)).StatusCode);   // still accepted
    }

    // ---- sign-out for the apps: the refresh token goes in the body ----

    [Fact]
    public async Task A_phone_signs_out_with_the_body_token_and_can_no_longer_refresh()
    {
        var token = await (await _client.CrewSignInAsync("DRV-01", api.DemoPassword, Ct)).RefreshTokenAsync(Ct);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.SignOutWithBodyAsync(token, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.RefreshWithBodyAsync(token, Ct)).StatusCode);
    }

    [Fact]
    public async Task A_cab_sign_out_ends_the_cab_session_and_refuses_its_access_token_at_once()
    {
        var signIn = await (await CabSignIn(_client, "CAB-01", "DRV-01")).ReadJsonAsync(Ct);
        var access = signIn.GetProperty("accessToken").GetString();
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync(MePath, access, Ct)).StatusCode);   // alive

        var response = await _client.SignOutWithBodyAsync(signIn.GetProperty("refreshToken").GetString(), Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var session = await InDb(db => db.CabSessions.AsNoTracking().SingleAsync(s => s.Id == signIn.GetProperty("crewSessionId").GetGuid(), Ct));
        Assert.NotNull(session.EndedAt);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(MePath, access, Ct)).StatusCode);   // the 15-minute token is dead too
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.RefreshWithBodyAsync(signIn.GetProperty("refreshToken").GetString(), Ct)).StatusCode);
    }

    [Fact]
    public async Task Signing_out_with_no_token_or_an_unknown_one_is_harmless_and_answers_204()
    {
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SignOutWithBodyAsync(null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SignOutWithBodyAsync("this-was-never-issued", Ct)).StatusCode);
    }

    private Task<int> SetDeviceActive(string code, bool active) =>
        InDb(async db =>
        {
            var device = await db.CabDevices.SingleAsync(d => d.Code == code, Ct);
            device.Active = active;
            return await db.SaveChangesAsync(Ct);
        });

    private Task<int> SetDriverActive(string staffNumber, bool active) =>
        InDb(async db =>
        {
            var account = await db.Users.SingleAsync(a => a.UserName == staffNumber, Ct);
            account.IsActive = active;
            return await db.SaveChangesAsync(Ct);
        });
}
