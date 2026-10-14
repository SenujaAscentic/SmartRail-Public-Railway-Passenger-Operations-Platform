using Microsoft.Extensions.Configuration;
using System.Text;

namespace SmartRail.Identity.Tokens;

// What is needed to sign and check access tokens. The key is a secret: it comes from user-secrets or an environment
// variable (Jwt:SigningKey), never from a file in the repository. Issuer and audience are fixed names, not secrets.
internal sealed record JwtSettings(string Issuer, string Audience, byte[] SigningKey)
{
    public const string DefaultIssuer = "smartrail";
    public const string DefaultAudience = "smartrail-api";

    // HS256 needs a key of at least 256 bits.
    private const int MinimumKeyBytes = 32;

    public static JwtSettings FromConfiguration(IConfiguration configuration)
    {
        var key = configuration["Jwt:SigningKey"];
        if (string.IsNullOrEmpty(key) || Encoding.UTF8.GetByteCount(key) < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is missing or shorter than 32 characters. Set it with: dotnet user-secrets set \"Jwt:SigningKey\" \"<at least 32 random characters>\" --project backend/src/SmartRail.Api");
        }

        return new JwtSettings(DefaultIssuer, DefaultAudience, Encoding.UTF8.GetBytes(key));
    }
}
