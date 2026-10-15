namespace SmartRail.Identity.Features.CabSignIn;

// The body of POST /auth/crew/cab-sign-in. The tablet's own credential travels in two headers (X-Device-Id, X-Device-Secret),
// never in the body or the URL. DeviceTime is the tablet's clock at the moment of sending (R-09).
public sealed record CabSignInRequest(string? StaffNumber, string? Password, DateTimeOffset? DeviceTime);

// Like the crew phone sign-in, plus the id of the new cab session and the server's time (the tablet can show how far off it is).
public sealed record CabSignInResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt,
    Guid CrewSessionId,
    DateTimeOffset ServerTime);
