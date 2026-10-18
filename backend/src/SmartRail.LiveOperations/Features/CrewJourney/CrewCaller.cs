using System.Globalization;
using System.Security.Claims;

namespace SmartRail.LiveOperations.Features.CrewJourney;

// Who is calling a crew-cab endpoint, read from the access token (contract section 2). The policy "CrewCab" has already checked the
// token, the device and that the cab session is still active (Identity does that on every request, R-19); the claims below are
// therefore trustworthy. The driver is the account id (`sub`), the same value IStaffDirectory gave the journey.
internal sealed record CrewCaller(Guid DriverId, Guid DeviceId, Guid CrewSessionId, TimeSpan ClockOffset)
{
    public static CrewCaller? From(ClaimsPrincipal user)
    {
        if (!Guid.TryParse(user.FindFirstValue("sub"), out var driver)
            || !Guid.TryParse(user.FindFirstValue("deviceId"), out var device)
            || !Guid.TryParse(user.FindFirstValue("crewSessionId"), out var session))
        {
            return null;
        }

        // The tablet's clock offset, measured at cab sign-in (R-09): server time minus device time, in milliseconds.
        var offset = long.TryParse(user.FindFirstValue("clockOffsetMs"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms)
            ? TimeSpan.FromMilliseconds(ms)
            : TimeSpan.Zero;
        return new CrewCaller(driver, device, session, offset);
    }
}
