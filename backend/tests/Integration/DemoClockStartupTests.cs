using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SmartRail.IntegrationTests.Identity;
using Xunit;

namespace SmartRail.IntegrationTests;

// The demo clock in the whole application: it cannot be turned on outside Development, and when it is on, sign-in, refresh and token
// validation keep working because they all run on that same clock (Identity asks the injected TimeProvider for every time).
[Collection("Api")]
public class DemoClockStartupTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string AllMessages(Exception ex) => string.Join(" | ", Chain(ex).Select(e => e.Message));

    private static IEnumerable<Exception> Chain(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException) yield return ex;
    }

    // ---- impossible outside Development ----

    [Theory]
    [InlineData("Production", "Clock:Start", "2026-10-20T05:55:00+05:30")]
    [InlineData("Production", "Clock:Speed", "10")]
    [InlineData("Staging", "Clock:Start", "2026-10-20T05:55:00+05:30")]
    public void The_application_does_not_start_outside_Development_with_a_Clock_setting(string environment, string setting, string value)
    {
        using var factory = api.CreateFactory(
            builder =>
            {
                builder.UseEnvironment(environment);
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { [setting] = value }));
            },
            useFixtureClock: false);

        var failure = Assert.ThrowsAny<Exception>(() => _ = factory.Server);

        Assert.Contains("Development", AllMessages(failure));
        Assert.Contains("Clock", AllMessages(failure));
    }

    // ---- sign-in, refresh and validation on a shifted clock ----

    [Fact]
    public async Task With_a_shifted_start_tokens_are_issued_validated_and_refreshed_on_the_demo_clock()
    {
        // A database of its own: an application whose clock says 2027 would otherwise generate 2027 journeys into the shared one.
        var database = await api.CreateDatabaseAsync("demo_clock_test");
        var start = new DateTimeOffset(2027, 3, 1, 5, 55, 0, TimeSpan.FromHours(5.5));
        using var factory = api.CreateFactory(
            builder => builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Clock:Start"] = start.ToString("o") })),
            useFixtureClock: false,
            connectionString: database);
        var client = factory.NewClient();

        // The Date header is the application's clock: it shows the demo time, not the machine's.
        var date = (await client.GetAsync("/openapi/v1.yaml", Ct)).Headers.Date!.Value;
        Assert.InRange(date, start.AddSeconds(-1), start.AddMinutes(2));

        // Staff sign-in: the token is issued on the demo clock ...
        var signIn = await client.SignInAsync("CTRL-01", api.DemoPassword, Ct);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var token = await signIn.AccessTokenAsync(Ct);
        var expires = AuthTestHelpers.Read(token).ValidTo;
        Assert.InRange(new DateTimeOffset(expires), start.AddMinutes(14), start.AddMinutes(16));   // 15 minutes after the DEMO start

        // ... and is believed on it. (On the machine's clock this token would not be valid yet: it is dated 2027.)
        var me = await client.GetAsync("/api/v1/staff/me", token, Ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        // A crew sign-in and refresh with the refresh token works as well.
        var crew = await client.CrewSignInAsync("DRV-01", api.DemoPassword, Ct);
        var refreshed = await client.RefreshWithBodyAsync(await crew.RefreshTokenAsync(Ct), Ct);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.False(string.IsNullOrEmpty((await refreshed.ReadJsonAsync(Ct)).GetProperty("accessToken").GetString()));
    }
}
