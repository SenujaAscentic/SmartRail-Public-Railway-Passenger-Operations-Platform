using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace SmartRail.Identity.Tokens;

// The body of POST /auth/refresh and POST /auth/sign-out for the crew apps, which keep the refresh token themselves.
public sealed record RefreshRequest(string? RefreshToken);

// Where a refresh token came from. Staff send it in the cookie and crew apps in the body, and each kind of token is
// accepted from ONE place only (answer 3, 15 Oct): a staff token in a body, or a crew token in a cookie, is refused.
internal readonly record struct PresentedToken(string? Secret, bool FromBody);

internal static class RefreshTokenSource
{
    public static async Task<PresentedToken> ReadAsync(HttpContext http, CancellationToken ct)
    {
        var fromBody = await ReadBodyTokenAsync(http, ct);
        return string.IsNullOrEmpty(fromBody)
            ? new PresentedToken(SessionCookie.Read(http), FromBody: false)
            : new PresentedToken(fromBody, FromBody: true);
    }

    // No body, no JSON content type or unreadable JSON all mean "no token in the body".
    private static async Task<string?> ReadBodyTokenAsync(HttpContext http, CancellationToken ct)
    {
        if (!http.Request.HasJsonContentType() || http.Request.ContentLength == 0)
        {
            return null;
        }

        try
        {
            return (await http.Request.ReadFromJsonAsync<RefreshRequest>(ct))?.RefreshToken;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
