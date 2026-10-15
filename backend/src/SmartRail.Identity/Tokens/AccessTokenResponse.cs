using System.Text.Json.Serialization;

namespace SmartRail.Identity.Tokens;

// What sign-in and refresh answer with. Staff get the access token only: their refresh token is in an HttpOnly cookie,
// never in the body. Crew apps have no cookie jar, so for them the refresh token (and its fixed end, 12 hours after
// sign-in) is in the body too; the two fields are left out of the JSON for staff.
public sealed record AccessTokenResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RefreshToken = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? RefreshExpiresAt = null);
