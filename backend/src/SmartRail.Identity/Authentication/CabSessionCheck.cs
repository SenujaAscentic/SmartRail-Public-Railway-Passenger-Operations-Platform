using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.Identity.Persistence;

namespace SmartRail.Identity.Authentication;

// An access token is stateless and stays valid for 15 minutes, but a cab session can end sooner: a newer sign-in by the
// same driver or on the same tablet (R-19), a sign-out, a deactivated device. So every request that carries a
// `crewSessionId` is checked against the session table (one indexed lookup). If the session is over, the token is refused
// at once, as if it were not signed in. Tokens without `crewSessionId` (staff, crew phone) are not affected.
internal static class CabSessionCheck
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var claim = context.Principal?.FindFirst("crewSessionId")?.Value;
        if (claim is null)
        {
            return;
        }

        if (!Guid.TryParse(claim, out var sessionId))
        {
            context.Fail("The cab session id is not valid.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<IdentityModuleDbContext>();
        var active = await db.CabSessions.AsNoTracking()
            .AnyAsync(s => s.Id == sessionId && s.EndedAt == null, context.HttpContext.RequestAborted);
        if (!active)
        {
            context.Fail("The cab session has ended.");
        }
    }
}
