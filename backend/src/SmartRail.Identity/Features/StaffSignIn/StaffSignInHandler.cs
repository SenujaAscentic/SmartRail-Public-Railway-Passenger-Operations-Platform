using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using SmartRail.Identity.Model;
using SmartRail.Identity.Persistence;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity.Features.StaffSignIn;

internal static class StaffSignInHandler
{
    public static async Task<IResult> HandleAsync(
        StaffSignInRequest request,
        HttpContext http,
        UserManager<StaffAccount> users,
        IPasswordHasher<StaffAccount> passwordHasher,
        IdentityModuleDbContext db,
        AccessTokenIssuer issuer,
        TimeProvider clock,
        CancellationToken ct)
    {
        var account = await users.FindByNameAsync(request.StaffNumber!.Trim());
        if (account is null)
        {
            DummyPasswordCheck.Run(passwordHasher, request.Password!);   // same work as a wrong password (open point 17)
            return Problems.SignInFailed(http);
        }

        // Identity counts failures and sets the lock; a locked account is refused even with the right password.
        if (await users.IsLockedOutAsync(account))
        {
            return Locked(http, account, clock);
        }

        if (!await users.CheckPasswordAsync(account, request.Password!))
        {
            await users.AccessFailedAsync(account);
            return await users.IsLockedOutAsync(account) ? Locked(http, account, clock) : Problems.SignInFailed(http);
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

        SessionCookie.Append(http, secret, row.ExpiresAt);
        return Results.Ok(new AccessTokenResponse(accessToken.Token, accessToken.ExpiresAt));
    }

    // How long until the account can be tried again. Never more than the lock itself lasts.
    private static IResult Locked(HttpContext http, StaffAccount account, TimeProvider clock)
    {
        var remaining = (account.LockoutEnd ?? clock.GetUtcNow()) - clock.GetUtcNow();
        var retryAfter = TimeSpan.FromTicks(Math.Clamp(remaining.Ticks, TimeSpan.FromSeconds(1).Ticks, IdentityModule.LockoutDuration.Ticks));
        return Problems.SignInLocked(http, retryAfter);
    }
}
