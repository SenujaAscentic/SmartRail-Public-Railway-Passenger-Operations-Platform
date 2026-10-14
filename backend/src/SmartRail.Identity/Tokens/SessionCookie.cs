using Microsoft.AspNetCore.Http;

namespace SmartRail.Identity.Tokens;

// The refresh-token cookie of the ops web app (contract section 2, C-3, threat T-14).
// HttpOnly: scripts cannot read it. Secure: https only. SameSite=Strict: other sites cannot make the browser send it.
// Path: sent only to the auth endpoints, not with every request.
internal static class SessionCookie
{
    public const string Name = "smartrail_refresh";
    private const string Path = "/api/v1/auth";

    public static void Append(HttpContext http, string secret, DateTimeOffset expires) =>
        http.Response.Cookies.Append(Name, secret, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = expires,
            IsEssential = true,
        });

    public static string? Read(HttpContext http) => http.Request.Cookies.TryGetValue(Name, out var value) ? value : null;
}
