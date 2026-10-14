using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartRail.Network;
using System.Text;
using Xunit;

namespace SmartRail.IntegrationTests.Identity;

// GET /api/v1/staff/me and how the API judges an access token   (contract 3.3, US-O1, threat T-04)
[Collection("Api")]
public class StaffMeTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string MePath = "/api/v1/staff/me";

    private readonly HttpClient _client = api.Factory.NewClient();

    [Fact]
    public async Task The_controller_sees_their_name_and_role_and_no_station()
    {
        var token = await api.TokenForAsync("Controller", Ct);

        var response = await _client.GetAsync(MePath, token, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync(Ct);
        Assert.Equal("Demo Controller", body.GetProperty("name").GetString());
        Assert.Equal("Controller", body.GetProperty("role").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, body.GetProperty("stationId").ValueKind);
    }

    [Fact]
    public async Task A_station_manager_sees_the_station_they_are_responsible_for()
    {
        using var scope = api.Factory.Services.CreateScope();
        var fot = (await scope.ServiceProvider.GetRequiredService<IStationDirectory>().GetStationIdsAsync(["FOT"], Ct))["FOT"];
        var token = await api.TokenForAsync("StationManager", Ct);

        var body = await (await _client.GetAsync(MePath, token, Ct)).ReadJsonAsync(Ct);

        Assert.Equal("StationManager", body.GetProperty("role").GetString());
        Assert.Equal(fot, body.GetProperty("stationId").GetGuid());
    }

    [Fact]
    public async Task Without_a_token_the_answer_is_401_Problem_Details_with_the_code_UNAUTHENTICATED()
    {
        var response = await _client.GetAsync(MePath, bearer: null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("UNAUTHENTICATED", await response.ProblemCodeAsync(Ct));
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString());
        Assert.False(string.IsNullOrEmpty((await response.ReadJsonAsync(Ct)).GetProperty("requestId").GetString()));
    }

    [Fact]
    public async Task A_role_that_is_not_allowed_gets_403_with_the_code_FORBIDDEN()
    {
        var driverToken = await api.TokenForAsync("Driver", Ct);

        var response = await _client.GetAsync(MePath, driverToken, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("FORBIDDEN", await response.ProblemCodeAsync(Ct));
    }

    [Fact]
    public async Task An_access_token_works_for_15_minutes_and_not_a_second_longer()
    {
        var clock = new FakeTimeProvider(api.Clock.GetUtcNow());
        using var factory = api.CreateFactory(builder => builder.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(clock)));
        var client = factory.NewClient();
        var token = await (await client.SignInAsync("CTRL-01", api.DemoPassword, Ct)).AccessTokenAsync(Ct);

        clock.Advance(TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(59));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(MePath, token, Ct)).StatusCode);

        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(MePath, token, Ct)).StatusCode);
    }

    [Fact]
    public async Task A_token_signed_with_some_other_key_is_refused()
    {
        var forged = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "smartrail",
            Audience = "smartrail-api",
            Expires = api.Clock.GetUtcNow().AddMinutes(10).UtcDateTime,
            Claims = new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString(), ["role"] = "Controller" },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("an-attackers-own-key-of-32-chars!!")), SecurityAlgorithms.HmacSha256),
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(MePath, forged, Ct)).StatusCode);
    }

    [Fact]
    public async Task A_token_with_no_signature_at_all_is_refused()
    {
        var unsigned = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "smartrail",
            Audience = "smartrail-api",
            Expires = api.Clock.GetUtcNow().AddMinutes(10).UtcDateTime,
            Claims = new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString(), ["role"] = "Controller" },
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(MePath, unsigned, Ct)).StatusCode);
    }
}
