namespace SmartRail.Identity.Authentication;

// The body of the password sign-ins: staff and crew phone (the cab sign-in adds the device time, see CabSignInRequest).
// Both fields are nullable so a missing field becomes a clear 400, not a binding error.
public sealed record PasswordSignInRequest(string? StaffNumber, string? Password);
