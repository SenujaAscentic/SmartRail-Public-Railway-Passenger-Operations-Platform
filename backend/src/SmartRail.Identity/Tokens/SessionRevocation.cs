using Microsoft.EntityFrameworkCore;
using SmartRail.Identity.Persistence;

namespace SmartRail.Identity.Tokens;

// Ends a sign-in session: every refresh token of its family stops working, including the newest one.
// Used when a copied token is detected (refresh) and when the person signs out.
internal static class SessionRevocation
{
    public static Task<int> RevokeAsync(IdentityModuleDbContext db, Guid familyId, DateTimeOffset now, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, now), ct);
}
