using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using SmartRail.Identity.Authentication;
using SmartRail.Identity.Model;
using SmartRail.Identity.Persistence;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity.Features.StaffSignIn;

internal static class StaffSignInHandler
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

        // Right password, but not an ops-web account (a driver, or a deactivated account): the same answer as a wrong password.
        if (!account.MaySignInToOpsWeb)
        {
            return Problems.SignInFailed(http);
        }

        await users.ResetAccessFailedCountAsync(account);

        var accessToken = issuer.Issue(account.Id, account.Role, account.StationId);
        var (secret, row) = RefreshTokens.Create(account.Id, familyId: Guid.NewGuid(), clock.GetUtcNow());   // a new session = a new family
        db.RefreshTokens.Add(row);
        await db.SaveChangesAsync(ct);

        // Staff keep the refresh token in an HttpOnly cookie, never in the body (C-3, T-14).
        SessionCookie.Append(http, secret, row.ExpiresAt);
        return Results.Ok(new AccessTokenResponse(accessToken.Token, accessToken.ExpiresAt));
    }
}
