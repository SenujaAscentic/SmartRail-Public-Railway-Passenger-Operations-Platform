using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using SmartRail.Identity.Model;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity.Authentication;

// The outcome of checking a staff number and password: either the account, or the answer to send back.
internal readonly record struct CredentialResult(StaffAccount? Account, IResult? Problem);

// The part of every password sign-in that is the same for staff, crew phone and cab: find the account, honour the lock,
// check the password, count failures. Whether this KIND of account may use this endpoint is the caller's business.
internal static class CredentialCheck
{
    public static async Task<CredentialResult> CheckAsync(
        string staffNumber,
        string password,
        HttpContext http,
        UserManager<StaffAccount> users,
        IPasswordHasher<StaffAccount> passwordHasher,
        TimeProvider clock)
    {
        var account = await users.FindByNameAsync(staffNumber.Trim());
        if (account is null)
        {
            DummyPasswordCheck.Run(passwordHasher, password);   // same work as a wrong password (open point 17)
            return new(null, Problems.SignInFailed(http));
        }

        // Identity counts failures and sets the lock; a locked account is refused even with the right password.
        if (await users.IsLockedOutAsync(account))
        {
            return new(null, Locked(http, account, clock));
        }

        if (!await users.CheckPasswordAsync(account, password))
        {
            await users.AccessFailedAsync(account);
            return new(null, await users.IsLockedOutAsync(account) ? Locked(http, account, clock) : Problems.SignInFailed(http));
        }

        return new(account, null);
    }

    // How long until the account can be tried again. Never more than the lock itself lasts.
    private static IResult Locked(HttpContext http, StaffAccount account, TimeProvider clock)
    {
        var remaining = (account.LockoutEnd ?? clock.GetUtcNow()) - clock.GetUtcNow();
        var retryAfter = TimeSpan.FromTicks(Math.Clamp(remaining.Ticks, TimeSpan.FromSeconds(1).Ticks, IdentityModule.LockoutDuration.Ticks));
        return Problems.SignInLocked(http, retryAfter);
    }
}
