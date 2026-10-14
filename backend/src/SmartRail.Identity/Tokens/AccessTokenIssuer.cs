using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartRail.Identity.Model;

namespace SmartRail.Identity.Tokens;

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAt);

// Creates the signed JWT access token (contract section 2). Claims: sub, aud, role, stationId?, exp. No personal data.
// Time comes from TimeProvider, so tests can fix it.
internal sealed class AccessTokenIssuer(JwtSettings settings, TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public IssuedAccessToken Issue(Guid accountId, StaffRole role, Guid? stationId)
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
