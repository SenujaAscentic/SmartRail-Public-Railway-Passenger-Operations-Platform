using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using SmartRail.Identity.Authentication;
using SmartRail.Identity.Model;
using SmartRail.Identity.Persistence;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity.Features.CrewSignIn;

// Phone mode (ADR-05): the driver's own phone. No cab session, no GPS, no journey actions; it only reads the roster.
internal static class CrewSignInHandler
{
    public static async Task<IResult> HandleAsync(
        PasswordSignInRequest request,
        HttpContext http,
        UserManager<StaffAccount> users,
        IPasswordHasher<StaffAccount> passwordHasher,
        IdentityModuleDbContext db,
        AccessTokenIssuer issuer,
        TimeProvider clock,
        CancellationToken ct)
    {
        var (account, problem) = await CredentialCheck.CheckAsync(request.StaffNumber!, request.Password!, http, users, passwordHasher, clock);
        if (account is null)
        {
            return problem!;
        }

        // Right password, but not a driver (a controller, a station manager, a deactivated account): the generic answer.
        if (!account.MayUseCrewApp)
        {
            return Problems.SignInFailed(http);
        }

        await users.ResetAccessFailedCountAsync(account);

        var now = clock.GetUtcNow();
        var accessToken = issuer.Issue(account.Id, account.Role, account.StationId);

        // A shift: the session ends 12 hours after sign-in, and refreshing does not extend it (answer 4, 15 Oct).
        var (secret, row) = RefreshTokens.Create(account.Id, familyId: Guid.NewGuid(), now, absoluteExpiresAt: now + RefreshTokens.CrewSessionLifetime);
        db.RefreshTokens.Add(row);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new AccessTokenResponse(accessToken.Token, accessToken.ExpiresAt, secret, row.ExpiresAt));
    }
}
