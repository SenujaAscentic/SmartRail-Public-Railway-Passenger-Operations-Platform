using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartRail.Identity.Model;

namespace SmartRail.Identity.Tokens;

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAt);

// What makes a token a CAB session token: the tablet, the session and the clock offset measured at sign-in (R-09).
internal sealed record CabSessionClaims(Guid DeviceId, Guid CrewSessionId, long ClockOffsetMs);

// Creates the signed JWT access token (contract section 2). Claims: sub, aud, role, stationId?, exp. No personal data.
// Time comes from TimeProvider, so tests can fix it.
internal sealed class AccessTokenIssuer(JwtSettings settings, TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public IssuedAccessToken Issue(Guid accountId, StaffRole role, Guid? stationId, CabSessionClaims? cab = null)
    {
        var now = clock.GetUtcNow();
        var expires = now + Lifetime;

        var claims = new Dictionary<string, object>
        {
            ["sub"] = accountId.ToString(),
            ["role"] = role.ToString(),
        };
        if (stationId is not null)
        {
            claims["stationId"] = stationId.Value.ToString();
        }

        if (cab is not null)
        {
            claims["deviceId"] = cab.DeviceId.ToString();
            claims["crewSessionId"] = cab.CrewSessionId.ToString();
            claims["clockOffsetMs"] = cab.ClockOffsetMs;   // a number, not text
        }

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(settings.SigningKey), SecurityAlgorithms.HmacSha256),
        });

        return new IssuedAccessToken(token, expires);
    }
}
