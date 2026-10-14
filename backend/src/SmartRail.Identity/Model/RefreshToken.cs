namespace SmartRail.Identity.Model;

// One refresh token. Every sign-in starts a "family"; each refresh uses up one token and issues the next in the same family.
// If a used token is presented again it was copied (threat T-14), so the whole family is revoked.
internal class RefreshToken
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public Guid AccountId { get; set; }

    // SHA-256 of the secret in the cookie. The secret itself is never stored.
    public string TokenHash { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsUsableAt(DateTimeOffset now) => UsedAt is null && RevokedAt is null && now < ExpiresAt;
}
