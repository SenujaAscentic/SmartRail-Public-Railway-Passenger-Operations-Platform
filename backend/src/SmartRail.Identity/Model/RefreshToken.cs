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

    // Set when the token was used a second time inside the grace window (see CanBeReplayedAt).
    public DateTimeOffset? ReplayedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    // Two browser tabs can send the same refresh token a moment apart. A token that was just replaced may be used
    // ONCE more within this time; any later or further use looks like a copy (threat T-14).
    public static readonly TimeSpan GraceWindow = TimeSpan.FromSeconds(10);

    public bool IsUsableAt(DateTimeOffset now) => UsedAt is null && RevokedAt is null && now < ExpiresAt;

    public bool CanBeReplayedAt(DateTimeOffset now) =>
        UsedAt is not null && ReplayedAt is null && RevokedAt is null && now - UsedAt.Value <= GraceWindow;
}
