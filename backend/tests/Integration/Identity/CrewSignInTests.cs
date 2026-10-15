using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.Identity.Persistence;
using Xunit;

namespace SmartRail.IntegrationTests.Identity;

// POST /api/v1/auth/crew/sign-in   (contract 3.4, US-C1, ADR-05)
// Phone mode: the driver's own phone. No GPS, no journey actions, and no cab session.
[Collection("Api")]
public class CrewSignInTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _client = api.Factory.NewClient();

    [Fact]
    public async Task A_driver_signs_in_and_gets_both_tokens_in_the_body_and_no_cookie()
    {
        var response = await _client.CrewSignInAsync("DRV-01", api.DemoPassword, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync(Ct);
        Assert.Equal(api.Clock.GetUtcNow().AddMinutes(15), body.GetProperty("expiresAt").GetDateTimeOffset());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("refreshToken").GetString()));
        Assert.Null(response.RefreshSetCookie());   // crew apps have no cookie jar, and staff must not be mixed up with them
    }

    [Fact]
    public async Task The_crew_session_lasts_exactly_12_hours_from_sign_in()
    {
        var body = await (await _client.CrewSignInAsync("DRV-01", api.DemoPassword, Ct)).ReadJsonAsync(Ct);

        Assert.Equal(api.Clock.GetUtcNow().AddHours(12), body.GetProperty("refreshExpiresAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task The_phone_token_is_a_Driver_token_without_any_cab_claims()
    {
        var jwt = AuthTestHelpers.Read(await (await _client.CrewSignInAsync("DRV-01", api.DemoPassword, Ct)).AccessTokenAsync(Ct));

        Assert.Equal("Driver", jwt.GetClaim("role").Value);
        Assert.False(jwt.TryGetClaim("crewSessionId", out _));
        Assert.False(jwt.TryGetClaim("deviceId", out _));
        Assert.False(jwt.TryGetClaim("stationId", out _));
    }

    [Fact]
    public async Task A_phone_sign_in_creates_no_cab_session()
    {
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var before = await db.CabSessions.CountAsync(Ct);

        await _client.CrewSignInAsync("DRV-04", api.DemoPassword, Ct);

        Assert.Equal(before, await db.CabSessions.CountAsync(Ct));
    }

    [Fact]
    public async Task Two_phone_sign_ins_of_the_same_driver_both_keep_working()
    {
        var first = await (await _client.CrewSignInAsync("DRV-04", api.DemoPassword, Ct)).RefreshTokenAsync(Ct);
        var second = await (await _client.CrewSignInAsync("DRV-04", api.DemoPassword, Ct)).RefreshTokenAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, (await _client.RefreshWithBodyAsync(first, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.RefreshWithBodyAsync(second, Ct)).StatusCode);
    }

    [Theory]
    [InlineData("CTRL-01")]
    [InlineData("SM-FOT")]
    public async Task Staff_accounts_cannot_use_the_crew_endpoint_and_get_the_same_generic_answer(string staffNumber)
    {
        var staff = await _client.CrewSignInAsync(staffNumber, api.DemoPassword, Ct);
        var wrongPassword = await _client.CrewSignInAsync("DRV-01", "not-the-password", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, staff.StatusCode);
        Assert.Equal("UNAUTHENTICATED", await staff.ProblemCodeAsync(Ct));
        var a = await staff.ReadJsonAsync(Ct);
        var b = await wrongPassword.ReadJsonAsync(Ct);
        Assert.Equal(b.GetProperty("title").GetString(), a.GetProperty("title").GetString());
        Assert.Equal(b.GetProperty("detail").GetString(), a.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_number_get_exactly_the_same_answer()
    {
        var wrongPassword = await _client.CrewSignInAsync("DRV-01", "not-the-password", Ct);
        var unknown = await _client.CrewSignInAsync("NOBODY-99", api.DemoPassword, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal((await wrongPassword.ReadJsonAsync(Ct)).GetProperty("detail").GetString(),
            (await unknown.ReadJsonAsync(Ct)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_driver_account()
    {
        try
        {
            for (var attempt = 1; attempt <= 4; attempt++)
            {
                Assert.Equal(HttpStatusCode.Unauthorized, (await _client.CrewSignInAsync("DRV-03", "wrong-" + attempt, Ct)).StatusCode);
            }

            var fifth = await _client.CrewSignInAsync("DRV-03", "wrong-5", Ct);
            Assert.Equal(HttpStatusCode.TooManyRequests, fifth.StatusCode);
            Assert.Equal("SIGN_IN_LOCKED", await fifth.ProblemCodeAsync(Ct));
            Assert.Equal(HttpStatusCode.TooManyRequests, (await _client.CrewSignInAsync("DRV-03", api.DemoPassword, Ct)).StatusCode);
        }
        finally
        {
            using var scope = api.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
            var account = await db.Users.SingleAsync(a => a.UserName == "DRV-03", Ct);
            account.LockoutEnd = null;
            account.AccessFailedCount = 0;
            await db.SaveChangesAsync(Ct);
        }
    }

    [Fact]
    public async Task A_deactivated_driver_cannot_sign_in()
    {
        await SetActive("DRV-02", false);
        try
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.CrewSignInAsync("DRV-02", api.DemoPassword, Ct)).StatusCode);
        }
        finally
        {
            await SetActive("DRV-02", true);
        }
    }

    [Theory]
    [InlineData(null, "x", "staffNumber")]
    [InlineData("DRV-01", "", "password")]
    public async Task A_missing_field_gets_a_400_naming_the_field(string? staffNumber, string? password, string field)
    {
        var response = await _client.PostAsJsonAsync(AuthTestHelpers.CrewSignInPath, new { staffNumber, password }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadJsonAsync(Ct)).GetProperty("errors").TryGetProperty(field, out _));
    }

    private async Task SetActive(string staffNumber, bool active)
    {
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var account = await db.Users.SingleAsync(a => a.UserName == staffNumber, Ct);
        account.IsActive = active;
        await db.SaveChangesAsync(Ct);
    }
}
