namespace SmartRail.Identity.Features.StaffSignIn;

// The body of POST /auth/staff/sign-in. Both fields are nullable so a missing field becomes a clear 400, not a binding error.
public sealed record StaffSignInRequest(string? StaffNumber, string? Password);
