using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartRail.Identity.Model;
using SmartRail.Identity.Persistence;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity.Features.RefreshSession;

// Swaps a refresh token for a new access token and a new refresh token ("rotation"). The token just replaced may be
// used once more within 10 seconds (two browser tabs); any later reuse ends the session.
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
        var token = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null)
        {
            return Problems.Unauthenticated(http);
        }

        // Revoked: the session was ended (copied token found, or signed out).
        if (token.RevokedAt is not null)
        {
            await SessionRevocation.RevokeAsync(db, token.FamilyId, now, ct);
            return Problems.Unauthenticated(http);
        }

        if (now >= token.ExpiresAt)
        {
            return Problems.Unauthenticated(http);   // idle for more than 30 minutes
        }

        // Use the token (or, inside the 10-second grace window, use it once more). If neither is possible, a copy exists
        // (threat T-14): end the whole session.
        if (!await TryUseAsync(db, token.Id, now, ct))
        {
            await SessionRevocation.RevokeAsync(db, token.FamilyId, now, ct);
            return Problems.Unauthenticated(http);
        }

        // A deactivated account loses its session at the next refresh, at most one access-token life (15 min) later.
        var account = await users.FindByIdAsync(token.AccountId.ToString());
        if (account is null || !account.MaySignInToOpsWeb)
        {
            await SessionRevocation.RevokeAsync(db, token.FamilyId, now, ct);
            return Problems.Unauthenticated(http);
        }

        var accessToken = issuer.Issue(account.Id, account.Role, account.StationId);
        var (newSecret, newRow) = RefreshTokens.Create(account.Id, token.FamilyId, now);   // same session, next token
        db.RefreshTokens.Add(newRow);
        await db.SaveChangesAsync(ct);

        SessionCookie.Append(http, newSecret, newRow.ExpiresAt);
        return Results.Ok(new AccessTokenResponse(accessToken.Token, accessToken.ExpiresAt));
    }

    // Each step is ONE database statement that only succeeds while its flag is still empty, so two refreshes at the same
    // instant cannot both win the same step: one gets the use, one gets the replay, a third is refused.
    private static async Task<bool> TryUseAsync(IdentityModuleDbContext db, Guid tokenId, DateTimeOffset now, CancellationToken ct)
    {
        var used = await db.RefreshTokens
            .Where(t => t.Id == tokenId && t.UsedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.UsedAt, now), ct);
        if (used == 1)
        {
            return true;
        }

        // Already used: read it fresh (another request may have just done it) and allow the one replay inside the window.
        var current = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.Id == tokenId, ct);
        if (!current.CanBeReplayedAt(now))
        {
            return false;
        }

        var replayed = await db.RefreshTokens
            .Where(t => t.Id == tokenId && t.ReplayedAt == null && t.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.ReplayedAt, now), ct);
        return replayed == 1;
    }
}
