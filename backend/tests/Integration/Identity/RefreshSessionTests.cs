using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SmartRail.Identity.Persistence;
using Xunit;

namespace SmartRail.IntegrationTests.Identity;

// POST /api/v1/auth/refresh   (contract 3.2, ADR-10, threat T-14)
// A refresh token is used once; using it gives a new access token and a new refresh token. Staff sessions end after 30 minutes idle.
[Collection("Api")]
public class RefreshSessionTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly HttpClient _client = api.Factory.NewClient();

    // An application of its own with its own clock, so a test can move time without disturbing the other tests.
    private (WebApplicationFactory<Program> Factory, FakeTimeProvider Clock) StartWithOwnClock()
    {
        var clock = new FakeTimeProvider(api.Clock.GetUtcNow());
        var factory = api.CreateFactory(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        return (factory, clock);
    }

    private async Task<string> SignInAndGetCookie(HttpClient client, string staffNumber = "CTRL-01") =>
        (await client.SignInAsync(staffNumber, api.DemoPassword, Ct)).RefreshCookieValue()!;

    [Fact]
    public async Task A_refresh_gives_a_new_access_token_and_a_different_refresh_cookie()
    {
        var cookie = await SignInAndGetCookie(_client);

        var response = await _client.RefreshAsync(cookie, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(await response.AccessTokenAsync(Ct)));
        var newCookie = response.RefreshCookieValue();
        Assert.NotNull(newCookie);
        Assert.NotEqual(cookie, newCookie);
        Assert.Contains("httponly", response.RefreshSetCookie()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_new_access_token_keeps_the_role_and_the_station()
    {
        var signIn = await _client.SignInAsync("SM-FOT", api.DemoPassword, Ct);
        var before = AuthTestHelpers.Read(await signIn.AccessTokenAsync(Ct));

        var refreshed = await _client.RefreshAsync(signIn.RefreshCookieValue(), Ct);

        var after = AuthTestHelpers.Read(await refreshed.AccessTokenAsync(Ct));
        Assert.Equal("StationManager", after.GetClaim("role").Value);
        Assert.Equal(before.GetClaim("stationId").Value, after.GetClaim("stationId").Value);
        Assert.Equal(before.Subject, after.Subject);
    }

    // ---- the 10-second grace window: two browser tabs may send the same token a moment apart ----

    [Fact]
    public async Task The_token_just_replaced_can_be_used_once_more_within_10_seconds_and_both_tabs_carry_on()
    {
        var (factory, clock) = StartWithOwnClock();
        using var _ = factory;
        var client = factory.NewClient();
        var first = await SignInAndGetCookie(client);

        var tabOne = await client.RefreshAsync(first, Ct);
        clock.Advance(TimeSpan.FromSeconds(5));
        var tabTwo = await client.RefreshAsync(first, Ct);   // the same, already replaced token, 5 s later

        Assert.Equal(HttpStatusCode.OK, tabOne.StatusCode);
        Assert.Equal(HttpStatusCode.OK, tabTwo.StatusCode);
        Assert.NotEqual(tabOne.RefreshCookieValue(), tabTwo.RefreshCookieValue());
        Assert.Equal(HttpStatusCode.OK, (await client.RefreshAsync(tabOne.RefreshCookieValue(), Ct)).StatusCode);   // each tab continues
        Assert.Equal(HttpStatusCode.OK, (await client.RefreshAsync(tabTwo.RefreshCookieValue(), Ct)).StatusCode);
    }

    [Fact]
    public async Task A_third_use_of_the_same_token_ends_the_whole_session_even_inside_the_window()
    {
        var (factory, clock) = StartWithOwnClock();
        using var _ = factory;
        var client = factory.NewClient();
        var first = await SignInAndGetCookie(client);
        var tabOne = await client.RefreshAsync(first, Ct);
        clock.Advance(TimeSpan.FromSeconds(2));
        var tabTwo = await client.RefreshAsync(first, Ct);
        clock.Advance(TimeSpan.FromSeconds(2));

        var third = await client.RefreshAsync(first, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, third.StatusCode);
        Assert.Equal("UNAUTHENTICATED", await third.ProblemCodeAsync(Ct));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.RefreshAsync(tabOne.RefreshCookieValue(), Ct)).StatusCode);   // the session is over
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.RefreshAsync(tabTwo.RefreshCookieValue(), Ct)).StatusCode);
    }

    [Fact]
    public async Task Using_a_replaced_token_after_the_10_seconds_is_refused_and_ends_the_whole_session()
    {
        var (factory, clock) = StartWithOwnClock();
        using var _ = factory;
        var client = factory.NewClient();
        var first = await SignInAndGetCookie(client);
        var firstRefresh = await client.RefreshAsync(first, Ct);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);   // the legitimate refresh
        clock.Advance(TimeSpan.FromSeconds(11));

        var replay = await client.RefreshAsync(first, Ct);                                   // a copied token is used 11 s later
        var legitimateNext = await client.RefreshAsync(firstRefresh.RefreshCookieValue(), Ct);   // so the genuine, newer token is dead too

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("UNAUTHENTICATED", await replay.ProblemCodeAsync(Ct));
        Assert.Equal(HttpStatusCode.Unauthorized, legitimateNext.StatusCode);
    }

    [Fact]
    public async Task Three_refreshes_at_the_same_moment_with_one_token_let_two_through_and_refuse_the_third()
    {
        var cookie = await SignInAndGetCookie(_client);

        var results = await Task.WhenAll(
            _client.RefreshAsync(cookie, Ct), _client.RefreshAsync(cookie, Ct), _client.RefreshAsync(cookie, Ct));

        Assert.Equal(2, results.Count(r => r.StatusCode == HttpStatusCode.OK));   // the use and the one allowed replay
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task A_refresh_without_the_cookie_is_refused()
    {
        var response = await _client.RefreshAsync(null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("UNAUTHENTICATED", await response.ProblemCodeAsync(Ct));
    }

    [Fact]
    public async Task A_refresh_with_a_made_up_token_is_refused()
    {
        var response = await _client.RefreshAsync("this-was-never-issued", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_session_that_was_idle_for_more_than_30_minutes_is_over()
    {
        var (factory, clock) = StartWithOwnClock();
        using var _ = factory;
        var client = factory.NewClient();
        var stillInTime = await SignInAndGetCookie(client);
        var tooLate = await SignInAndGetCookie(client);   // a second session, signed in at the same moment

        clock.Advance(TimeSpan.FromMinutes(29));
        Assert.Equal(HttpStatusCode.OK, (await client.RefreshAsync(stillInTime, Ct)).StatusCode);   // 29 minutes: still fine

        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.RefreshAsync(tooLate, Ct)).StatusCode);   // 31 minutes: over
    }

    [Fact]
    public async Task Every_refresh_starts_a_new_30_minutes_so_an_active_user_stays_signed_in()
    {
        var (factory, clock) = StartWithOwnClock();
        using var _ = factory;
        var client = factory.NewClient();
        var cookie = await SignInAndGetCookie(client);

        clock.Advance(TimeSpan.FromMinutes(20));
        var first = await client.RefreshAsync(cookie, Ct);
        clock.Advance(TimeSpan.FromMinutes(20));   // 40 minutes after sign-in, but only 20 after the last refresh
        var second = await client.RefreshAsync(first.RefreshCookieValue(), Ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task A_deactivated_account_is_refused_at_its_next_refresh()
    {
        var whileActive = await SignInAndGetCookie(_client, "SM-PLG");
        var afterDeactivation = await SignInAndGetCookie(_client, "SM-PLG");
        Assert.Equal(HttpStatusCode.OK, (await _client.RefreshAsync(whileActive, Ct)).StatusCode);   // control: refresh works while active

        await SetActive("SM-PLG", false);
        try
        {
            var response = await _client.RefreshAsync(afterDeactivation, Ct);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await SetActive("SM-PLG", true);
        }
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
