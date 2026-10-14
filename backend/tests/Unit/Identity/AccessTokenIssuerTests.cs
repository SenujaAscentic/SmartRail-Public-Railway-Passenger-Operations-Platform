using System.Security.Cryptography;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using SmartRail.Identity.Model;
using SmartRail.Identity.Tokens;
using Xunit;

namespace SmartRail.UnitTests.Identity;

// The access token (JWT) is the 15-minute pass sent with every request (contract section 2, ADR-10).
public class AccessTokenIssuerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 4, 30, 0, TimeSpan.Zero);
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid StationId = Guid.NewGuid();

    private static AccessTokenIssuer Create()
    {
        var settings = new JwtSettings("smartrail", "smartrail-api", RandomNumberGenerator.GetBytes(32));
        return new AccessTokenIssuer(settings, new FakeTimeProvider(Now));
    }

    private static JsonWebToken Read(string token) => new JsonWebTokenHandler().ReadJsonWebToken(token);

    [Fact]
    public void The_token_expires_15_minutes_after_it_is_issued()
    {
        var issued = Create().Issue(AccountId, StaffRole.Controller, stationId: null);

        Assert.Equal(Now.AddMinutes(15), issued.ExpiresAt);
        Assert.Equal(Now.AddMinutes(15), new DateTimeOffset(Read(issued.Token).ValidTo));
    }

    [Fact]
    public void The_token_carries_subject_role_audience_and_issuer()
    {
        var jwt = Read(Create().Issue(AccountId, StaffRole.Controller, stationId: null).Token);

        Assert.Equal(AccountId.ToString(), jwt.Subject);
        Assert.Equal("Controller", jwt.GetClaim("role").Value);
        Assert.Contains("smartrail-api", jwt.Audiences);
        Assert.Equal("smartrail", jwt.Issuer);
    }

    [Fact]
    public void A_controller_token_has_no_stationId_because_the_controller_covers_the_whole_line()
    {
        var jwt = Read(Create().Issue(AccountId, StaffRole.Controller, stationId: null).Token);

        Assert.False(jwt.TryGetClaim("stationId", out _));
    }

    [Fact]
    public void A_station_manager_token_carries_the_station_id()
    {
        var jwt = Read(Create().Issue(AccountId, StaffRole.StationManager, StationId).Token);

        Assert.Equal(StationId.ToString(), jwt.GetClaim("stationId").Value);
    }

    [Fact]
    public void The_token_holds_no_personal_data_only_the_agreed_claims()
    {
        var agreed = new HashSet<string> { "sub", "role", "stationId", "aud", "iss", "iat", "nbf", "exp" };

        var jwt = Read(Create().Issue(AccountId, StaffRole.StationManager, StationId).Token);

        Assert.All(jwt.Claims, claim => Assert.Contains(claim.Type, agreed));
    }
}
