using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartRail.Identity.Persistence;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity.Features.SignOut;

// Ends the session that the refresh token belongs to (cookie for staff, body for crew apps) and tells the browser to
// delete the cookie. For a cab session it ends the cab session too, so its access token stops working at once.
// Always answers 204, even with no token or an unknown one: signing out has nothing to refuse, and the answer
// must not reveal whether a token was real.
internal static class SignOutHandler
{
    public static async Task<IResult> HandleAsync(HttpContext http, IdentityModuleDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var presented = await RefreshTokenSource.ReadAsync(http, ct);
        if (!string.IsNullOrEmpty(presented.Secret))
        {
            var hash = RefreshTokens.Hash(presented.Secret);
            var token = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
            if (token is not null)
            {
                var now = clock.GetUtcNow();

                // The whole session, so a token still inside the grace window cannot bring it back.
                await SessionRevocation.RevokeAsync(db, token.FamilyId, now, ct);

                // The app calls sign-out after "End journey" for now; the JourneyEnded event will end the session later.
                if (token.CabSessionId is { } sessionId)
                {
                    await db.CabSessions
                        .Where(s => s.Id == sessionId && s.EndedAt == null)
                        .ExecuteUpdateAsync(set => set.SetProperty(s => s.EndedAt, now), ct);
                }
            }
        }

        SessionCookie.Clear(http);
        return Results.NoContent();
    }
}
