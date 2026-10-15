using SmartRail.Identity.Authentication;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity.Features.CabSignIn;

internal static class CabSignInValidator
{
    public const string DeviceIdHeader = "X-Device-Id";
    public const string DeviceSecretHeader = "X-Device-Secret";

    // Returns the field messages, or an empty dictionary when the request is acceptable. Header problems are named by header.
    // The "more than 24 hours" rule (R-09) needs the server's time, which is passed in; nothing here reads a clock.
    public static Dictionary<string, string[]> Validate(string? deviceId, string? deviceSecret, CabSignInRequest? request, DateTimeOffset serverTime)
    {
        var errors = PasswordSignInValidator.Validate(request?.StaffNumber, request?.Password);

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            errors[DeviceIdHeader] = ["Send the tablet's id in the X-Device-Id header."];
        }

        if (string.IsNullOrEmpty(deviceSecret))
        {
            errors[DeviceSecretHeader] = ["Send the tablet's secret in the X-Device-Secret header."];
        }

        if (request?.DeviceTime is null)
        {
            errors["deviceTime"] = ["Send the tablet's current date and time."];
        }
        else if (!ClockOffset.TryMeasure(serverTime, request.DeviceTime.Value, out _))
        {
            errors["deviceTime"] = ["The tablet's clock is more than 24 hours different from the server's. Correct the date and time on the tablet."];
        }

        return errors;
    }
}
