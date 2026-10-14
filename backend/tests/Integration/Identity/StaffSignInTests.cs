using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.Identity.Persistence;
using SmartRail.Network;
using Xunit;

namespace SmartRail.IntegrationTests.Identity;

// POST /api/v1/auth/staff/sign-in   (contract 3.3, US-O1, threat T-01)
[Collection("Api")]
public class StaffSignInTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _client = api.Factory.NewClient();

    private async Task ChangeAccount(string staffNumber, Action<SmartRail.Identity.Model.StaffAccount> change)
    {
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var account = await db.Users.SingleAsync(a => a.UserName == staffNumber, Ct);
        change(account);
        await db.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task The_controller_signs_in_and_gets_a_15_minute_access_token()
    {
        var response = await _client.SignInAsync("CTRL-01", api.DemoPassword, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync(Ct);
        Assert.Equal(api.Clock.GetUtcNow().AddMinutes(15), body.GetProperty("expiresAt").GetDateTimeOffset());
        var jwt = AuthTestHelpers.Read(body.GetProperty("accessToken").GetString()!);
        Assert.Equal("Controller", jwt.GetClaim("role").Value);
        Assert.False(jwt.TryGetClaim("stationId", out _));
    }

    [Fact]
    public async Task The_staff_number_is_not_case_sensitive()
    {
        var response = await _client.SignInAsync("ctrl-01", api.DemoPassword, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_station_manager_gets_a_token_with_the_station_id_of_their_station()
    {
        using var scope = api.Factory.Services.CreateScope();
        var fot = (await scope.ServiceProvider.GetRequiredService<IStationDirectory>().GetStationIdsAsync(["FOT"], Ct))["FOT"];

        var response = await _client.SignInAsync("SM-FOT", api.DemoPassword, Ct);

        var jwt = AuthTestHelpers.Read(await response.AccessTokenAsync(Ct));
        Assert.Equal("StationManager", jwt.GetClaim("role").Value);
        Assert.Equal(fot.ToString(), jwt.GetClaim("stationId").Value);
    }

    [Fact]
    public async Task The_refresh_token_travels_only_in_an_HttpOnly_Secure_SameSite_Strict_cookie()
    {
        var response = await _client.SignInAsync("CTRL-01", api.DemoPassword, Ct);

        var cookie = response.RefreshSetCookie();
        Assert.NotNull(cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.OrdinalIgnoreCase);

        var secret = response.RefreshCookieValue()!;
        Assert.DoesNotContain(secret, await response.Content.ReadAsStringAsync(Ct));   // not in the body, where a script could read it
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_staff_number_get_exactly_the_same_answer()
    {
        var wrongPassword = await _client.SignInAsync("CTRL-01", "not-the-password", Ct);
        var unknownNumber = await _client.SignInAsync("NOBODY-99", api.DemoPassword, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownNumber.StatusCode);
        Assert.Equal("UNAUTHENTICATED", await wrongPassword.ProblemCodeAsync(Ct));

        var first = await wrongPassword.ReadJsonAsync(Ct);
        var second = await unknownNumber.ReadJsonAsync(Ct);
        Assert.Equal(first.GetProperty("title").GetString(), second.GetProperty("title").GetString());
        Assert.Equal(first.GetProperty("detail").GetString(), second.GetProperty("detail").GetString());
        Assert.Null(wrongPassword.RefreshSetCookie());
    }

    [Fact]
    public async Task A_driver_cannot_sign_in_here_even_with_the_right_password_and_gets_the_same_generic_answer()
    {
        var driver = await _client.SignInAsync("DRV-01", api.DemoPassword, Ct);
        var wrongPassword = await _client.SignInAsync("CTRL-01", "not-the-password", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, driver.StatusCode);
        Assert.Equal("UNAUTHENTICATED", await driver.ProblemCodeAsync(Ct));
        Assert.Null(driver.RefreshSetCookie());

        var fromDriver = await driver.ReadJsonAsync(Ct);
        var fromWrongPassword = await wrongPassword.ReadJsonAsync(Ct);
        Assert.Equal(fromWrongPassword.GetProperty("title").GetString(), fromDriver.GetProperty("title").GetString());
        Assert.Equal(fromWrongPassword.GetProperty("detail").GetString(), fromDriver.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_deactivated_account_cannot_sign_in_even_with_the_right_password()
    {
        await ChangeAccount("SM-PLG", a => a.IsActive = false);
        try
        {
            var response = await _client.SignInAsync("SM-PLG", api.DemoPassword, Ct);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await ChangeAccount("SM-PLG", a => a.IsActive = true);
        }
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_and_the_right_password_is_then_refused_too()
    {
        try
        {
            for (var attempt = 1; attempt <= 4; attempt++)
            {
                var failed = await _client.SignInAsync("SM-KDT", "wrong-" + attempt, Ct);
                Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
            }

            var fifth = await _client.SignInAsync("SM-KDT", "wrong-5", Ct);
            Assert.Equal(HttpStatusCode.TooManyRequests, fifth.StatusCode);
            Assert.Equal("SIGN_IN_LOCKED", await fifth.ProblemCodeAsync(Ct));
            var retryAfter = fifth.Headers.RetryAfter!.Delta!.Value;
            Assert.InRange(retryAfter.TotalMinutes, 14, 15);

            var withRightPassword = await _client.SignInAsync("SM-KDT", api.DemoPassword, Ct);
            Assert.Equal(HttpStatusCode.TooManyRequests, withRightPassword.StatusCode);
        }
        finally
        {
            await ChangeAccount("SM-KDT", a => { a.LockoutEnd = null; a.AccessFailedCount = 0; });
        }
    }

    [Fact]
    public async Task A_successful_sign_in_resets_the_count_of_wrong_passwords()
    {
        try
        {
            for (var attempt = 1; attempt <= 4; attempt++)
            {
                await _client.SignInAsync("SM-PLG", "wrong", Ct);
            }

            Assert.Equal(HttpStatusCode.OK, (await _client.SignInAsync("SM-PLG", api.DemoPassword, Ct)).StatusCode);

            for (var attempt = 1; attempt <= 4; attempt++)
            {
                var failed = await _client.SignInAsync("SM-PLG", "wrong", Ct);
                Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);   // 4 again, not locked: the count started over
            }
        }
        finally
        {
            await ChangeAccount("SM-PLG", a => { a.AccessFailedCount = 0; a.LockoutEnd = null; });
        }
    }

    [Fact]
    public async Task An_absurdly_long_password_is_refused_before_it_is_hashed()
    {
        var response = await _client.SignInAsync("CTRL-01", new string('x', 201), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadJsonAsync(Ct)).GetProperty("errors").TryGetProperty("password", out _));
    }

    [Theory]
    [InlineData(null, "x", "staffNumber")]
    [InlineData("", "x", "staffNumber")]
    [InlineData("CTRL-01", null, "password")]
    [InlineData("CTRL-01", "", "password")]
    public async Task A_missing_field_gets_a_400_naming_the_field(string? staffNumber, string? password, string field)
    {
        var response = await _client.PostAsJsonAsync(AuthTestHelpers.SignInPath, new { staffNumber, password }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await response.ProblemCodeAsync(Ct));
        var errors = (await response.ReadJsonAsync(Ct)).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _), $"The errors should name {field}.");
    }
}
