using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using SmartRail.Identity.Model;

namespace SmartRail.Identity.Tokens;

// Creating and hashing refresh-token secrets.
internal static class RefreshTokens
{
    // Staff sessions end after 30 minutes without a refresh (US-O1). Every refresh starts a new 30 minutes (sliding).
    public static readonly TimeSpan IdleLifetime = TimeSpan.FromMinutes(30);

    // Returns the secret (goes into the cookie, once) and the row to store (holds only the hash).
    public static (string Secret, RefreshToken Row) Create(Guid accountId, Guid familyId, DateTimeOffset now)
    {
        var secret = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));   // 256 random bits: cannot be guessed

        var row = new RefreshToken
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            AccountId = accountId,
            TokenHash = Hash(secret),
            CreatedAt = now,
            ExpiresAt = now + IdleLifetime,
        };
        return (secret, row);
    }

    // A plain SHA-256 is right here: the secret is 256 random bits, so unlike a password it cannot be guessed offline.
    // (Passwords and the cab-device secret use Identity's PasswordHasher instead.)
    public static string Hash(string secret) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
