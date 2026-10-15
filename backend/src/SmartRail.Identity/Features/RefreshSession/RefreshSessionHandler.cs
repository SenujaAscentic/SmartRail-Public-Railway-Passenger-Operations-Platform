using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartRail.Identity.Model;
using SmartRail.Identity.Persistence;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity.Features.RefreshSession;

// Swaps a refresh token for a new access token and a new refresh token ("rotation"). The token just replaced may be
// used once more within 10 seconds (two browser tabs); any later reuse ends the session.
// Staff send the token in the cookie, crew apps in the body. Every failure is the same 401, so a caller learns nothing about why.
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
        var presented = await RefreshTokenSource.ReadAsync(http, ct);
        if (string.IsNullOrEmpty(presented.Secret))
        {
            return Problems.Unauthenticated(http);
        }

        var now = clock.GetUtcNow();
        var hash = RefreshTokens.Hash(presented.Secret);
        var token = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null)
        {
            return Problems.Unauthenticated(http);
        }

        var account = await users.FindByIdAsync(token.AccountId.ToString());

        // Each kind of token is accepted from one place only: crew (drivers) from the body, staff from the cookie.
        // A mismatch changes nothing: it is most likely an app that is configured wrongly.
        if (account is not null && presented.FromBody != (account.Role == StaffRole.Driver))
        {
            return Problems.Unauthenticated(http);
        }

        // Revoked: the session was ended (copied token found, signed out, or a newer cab sign-in).
        if (token.RevokedAt is not null)
        {
            await SessionRevocation.RevokeAsync(db, token.FamilyId, now, ct);
            return Problems.Unauthenticated(http);
        }

        if (now >= token.ExpiresAt)
        {
            return Problems.Unauthenticated(http);   // idle for more than 30 minutes (staff) or past the 12-hour shift (crew)
        }

        // Use the token (or, inside the 10-second grace window, use it once more). If neither is possible, a copy exists
        // (threat T-14): end the whole session.
        if (!await TryUseAsync(db, token.Id, now, ct))
        {
            await SessionRevocation.RevokeAsync(db, token.FamilyId, now, ct);
            return Problems.Unauthenticated(http);
        }

        // A deactivated account loses its session at the next refresh, at most one access-token life (15 min) later.
        var allowed = account is not null && (account.Role == StaffRole.Driver ? account.MayUseCrewApp : account.MaySignInToOpsWeb);
        if (!allowed)
        {
            await SessionRevocation.RevokeAsync(db, token.FamilyId, now, ct);
            return Problems.Unauthenticated(http);
        }

        // A cab session must still be active and its tablet still registered and active (threat T-12): a deactivated
        // device is refused at its next refresh.
        CabSessionClaims? cab = null;
        if (token.CabSessionId is { } sessionId)
        {
            var session = await db.CabSessions.SingleOrDefaultAsync(s => s.Id == sessionId, ct);
            var deviceActive = session is not null && await db.CabDevices.AsNoTracking().AnyAsync(d => d.Id == session.DeviceId && d.Active, ct);
            if (session is null || session.EndedAt is not null || !deviceActive)
            {
                if (session is { EndedAt: null })
                {
                    session.EndedAt = now;
                    await db.SaveChangesAsync(ct);
                }

                await SessionRevocation.RevokeAsync(db, token.FamilyId, now, ct);
                return Problems.Unauthenticated(http);
            }

            cab = new CabSessionClaims(session.DeviceId, session.Id, session.ClockOffsetMs);
        }

        var accessToken = issuer.Issue(account!.Id, account.Role, account.StationId, cab);
        var (newSecret, newRow) = RefreshTokens.Next(token, now);   // same session, next token (a crew session keeps its fixed end)
        db.RefreshTokens.Add(newRow);
        await db.SaveChangesAsync(ct);

        if (presented.FromBody)
        {
            return Results.Ok(new AccessTokenResponse(accessToken.Token, accessToken.ExpiresAt, newSecret, newRow.ExpiresAt));
        }

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
