using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartRail.Identity.Model;
using SmartRail.Identity.Persistence;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity.Features.RefreshSession;

// Swaps a refresh token for a new access token and a new refresh token ("rotation").
// Every failure is the same 401, so a caller learns nothing about why.
internal static class RefreshSessionHandler
{
    public static async Task<IResult> HandleAsync(
        HttpContext http,
        IdentityModuleDbContext db,
        UserManager<StaffAccount> users,
        AccessTokenIssuer issuer,
        TimeProvider clock,
        CancellationToken ct)
    {
        var secret = SessionCookie.Read(http);
        if (string.IsNullOrEmpty(secret))
        {
            return Problems.Unauthenticated(http);
        }

        var now = clock.GetUtcNow();
        var hash = RefreshTokens.Hash(secret);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null)
        {
            return Problems.Unauthenticated(http);
        }

        // A used or revoked token coming back means the secret was copied (threat T-14): end the whole session.
        if (token.UsedAt is not null || token.RevokedAt is not null)
        {
            await RevokeSessionAsync(db, token.FamilyId, now, ct);
            return Problems.Unauthenticated(http);
        }

        if (!token.IsUsableAt(now))
        {
            return Problems.Unauthenticated(http);   // idle for more than 30 minutes
        }

        // Use the token up. One database statement that only succeeds while UsedAt is still empty,
        // so two refreshes at the same instant cannot both win.
        var claimed = await db.RefreshTokens
            .Where(t => t.Id == token.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.UsedAt, now), ct);
        if (claimed == 0)
        {
            await RevokeSessionAsync(db, token.FamilyId, now, ct);
            return Problems.Unauthenticated(http);
        }

        // A deactivated account loses its session at the next refresh, at most one access-token life (15 min) later.
        var account = await users.FindByIdAsync(token.AccountId.ToString());
        if (account is null || !account.MaySignInToOpsWeb)
        {
            await RevokeSessionAsync(db, token.FamilyId, now, ct);
            return Problems.Unauthenticated(http);
        }

        var accessToken = issuer.Issue(account.Id, account.Role, account.StationId);
        var (newSecret, newRow) = RefreshTokens.Create(account.Id, token.FamilyId, now);   // same session, next token
        db.RefreshTokens.Add(newRow);
        await db.SaveChangesAsync(ct);

        SessionCookie.Append(http, newSecret, newRow.ExpiresAt);
        return Results.Ok(new AccessTokenResponse(accessToken.Token, accessToken.ExpiresAt));
    }

    private static Task<int> RevokeSessionAsync(IdentityModuleDbContext db, Guid familyId, DateTimeOffset now, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, now), ct);
}
