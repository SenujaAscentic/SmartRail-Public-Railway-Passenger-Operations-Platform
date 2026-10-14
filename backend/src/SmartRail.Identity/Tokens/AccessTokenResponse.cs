namespace SmartRail.Identity.Tokens;

// What sign-in and refresh answer with: the access token only. The refresh token is in an HttpOnly cookie, never in the body.
public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
