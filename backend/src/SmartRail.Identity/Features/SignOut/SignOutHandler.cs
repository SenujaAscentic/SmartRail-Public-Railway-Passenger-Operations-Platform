using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartRail.Identity.Persistence;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity.Features.SignOut;

// Ends the session that the refresh cookie belongs to and tells the browser to delete the cookie.
// Always answers 204, even with no cookie or an unknown one: signing out has nothing to refuse, and the answer
// must not reveal whether a token was real.
internal static class SignOutHandler
{
    public static async Task<IResult> HandleAsync(HttpContext http, IdentityModuleDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var secret = SessionCookie.Read(http);
        if (!string.IsNullOrEmpty(secret))
        {
            var hash = RefreshTokens.Hash(secret);
            var token = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
            if (token is not null)
            {
                // The whole session, so a token still inside the grace window cannot bring it back.
                await SessionRevocation.RevokeAsync(db, token.FamilyId, clock.GetUtcNow(), ct);
            }
        }

        SessionCookie.Clear(http);
        return Results.NoContent();
    }
}
