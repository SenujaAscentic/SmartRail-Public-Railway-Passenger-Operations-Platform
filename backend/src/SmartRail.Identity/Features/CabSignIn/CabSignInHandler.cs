using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartRail.Identity.Authentication;
using SmartRail.Identity.Model;
using SmartRail.Identity.Persistence;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity.Features.CabSignIn;

// Cab mode (ADR-05): a registered tablet in the cab plus the driver. Two things are proven, in this order:
//   1. the TABLET, by its device credential (headers);  2. the DRIVER, by staff number and password.
// The tablet is checked first on purpose: a stolen or wrong tablet credential must never be able to run up the failed-password
// count of a real driver and lock them out.
internal static class CabSignInHandler
{
    private static readonly PasswordHasher<CabDevice> DeviceHasher = new();

    public static async Task<IResult> HandleAsync(
        string deviceId,
        string deviceSecret,
        CabSignInRequest request,
        HttpContext http,
        UserManager<StaffAccount> users,
        IPasswordHasher<StaffAccount> passwordHasher,
        IdentityModuleDbContext db,
        AccessTokenIssuer issuer,
        TimeProvider clock,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        // 1. The tablet. An unknown device, a deactivated one and a wrong secret all give the same generic 401 (contract
        //    section 6, threat T-12), and all do one password-hash check, so none of them is faster than the others.
        var device = await db.CabDevices.AsNoTracking().SingleOrDefaultAsync(d => d.Code == deviceId.Trim(), ct);
        var secretMatches = device is null
            ? DummyPasswordCheck.RunDevice(deviceSecret)
            : DeviceHasher.VerifyHashedPassword(device, device.CredentialHash, deviceSecret) != PasswordVerificationResult.Failed;
        if (device is null || !device.Active || !secretMatches)
        {
            return Problems.SignInFailed(http);
        }

        // 2. The driver.
        var (account, problem) = await CredentialCheck.CheckAsync(request.StaffNumber!, request.Password!, http, users, passwordHasher, clock);
        if (account is null)
        {
            return problem!;
        }

        if (!account.MayUseCrewApp)
        {
            return Problems.SignInFailed(http);   // a controller or a station manager on a tablet: the generic answer
        }

        await users.ResetAccessFailedCountAsync(account);

        // R-09: measured here, once. (The validator already refused anything more than 24 hours off.)
        ClockOffset.TryMeasure(now, request.DeviceTime!.Value, out var offsetMs);

        // R-19: one active cab session per driver, and one per tablet. A new sign-in ends the older ones, in one transaction.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var older = await db.CabSessions
                .Where(s => s.EndedAt == null && (s.AccountId == account.Id || s.DeviceId == device.Id))
                .ToListAsync(ct);
            foreach (var session in older)
            {
                session.EndedAt = now;
            }

            await db.SaveChangesAsync(ct);
            var olderIds = older.Select(s => s.Id).ToList();
            await db.RefreshTokens
                .Where(t => t.CabSessionId != null && olderIds.Contains(t.CabSessionId.Value) && t.RevokedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, now), ct);

            var newSession = new CabSession { Id = Guid.NewGuid(), AccountId = account.Id, DeviceId = device.Id, StartedAt = now, ClockOffsetMs = offsetMs };
            db.CabSessions.Add(newSession);

            // A shift: 12 hours from sign-in, fixed (answer 4, 15 Oct).
            var (secret, row) = RefreshTokens.Create(account.Id, familyId: Guid.NewGuid(), now, now + RefreshTokens.CrewSessionLifetime, newSession.Id);
            db.RefreshTokens.Add(row);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var accessToken = issuer.Issue(account.Id, account.Role, account.StationId, new CabSessionClaims(device.Id, newSession.Id, offsetMs));
            return Results.Ok(new CabSignInResponse(accessToken.Token, accessToken.ExpiresAt, secret, row.ExpiresAt, newSession.Id, now));
        }
        catch (DbUpdateException)
        {
            // Two sign-ins of the same driver (or on the same tablet) at the same instant: the database allowed only one active
            // session, so this one lost the race. Nothing was changed; the app simply tries again.
            return Problems.Conflict(http, "Another sign-in for this driver or tablet is in progress. Try again.");
        }
    }
}
