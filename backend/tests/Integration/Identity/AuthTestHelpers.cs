using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using SmartRail.Identity.Model;
using SmartRail.Identity.Tokens;

namespace SmartRail.IntegrationTests.Identity;

// Small helpers shared by the sign-in, refresh and permission tests.
internal static class AuthTestHelpers
{
    public const string SignInPath = "/api/v1/auth/staff/sign-in";
    public const string RefreshPath = "/api/v1/auth/refresh";
    public const string CookieName = "smartrail_refresh";
    public const string CrewSignInPath = "/api/v1/auth/crew/sign-in";
    public const string CabSignInPath = "/api/v1/auth/crew/cab-sign-in";

    // The refresh cookie is Secure, and the test server is plain http. Cookies are therefore handled by hand
    // (read from Set-Cookie, sent in a Cookie header) so the tests control exactly what the browser would send.
    public static HttpClient NewClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    public static Task<HttpResponseMessage> SignInAsync(this HttpClient client, string staffNumber, string password, CancellationToken ct) =>
        client.PostAsJsonAsync(SignInPath, new { staffNumber, password }, ct);

    public static Task<HttpResponseMessage> CrewSignInAsync(this HttpClient client, string staffNumber, string password, CancellationToken ct) =>
        client.PostAsJsonAsync(CrewSignInPath, new { staffNumber, password }, ct);

    // The tablet sends its credential in two headers and the driver's details in the body (contract 3.4).
    public static Task<HttpResponseMessage> CabSignInAsync(
        this HttpClient client, string? deviceId, string? deviceSecret, string staffNumber, string password, DateTimeOffset? deviceTime, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, CabSignInPath)
        {
            Content = JsonContent.Create(new { staffNumber, password, deviceTime }),
        };
        if (deviceId is not null) request.Headers.Add("X-Device-Id", deviceId);
        if (deviceSecret is not null) request.Headers.Add("X-Device-Secret", deviceSecret);
        return client.SendAsync(request, ct);
    }

    // Crew apps keep the refresh token themselves and send it in the body (staff use the cookie).
    public static Task<HttpResponseMessage> RefreshWithBodyAsync(this HttpClient client, string? refreshToken, CancellationToken ct) =>
        client.PostAsJsonAsync(RefreshPath, new { refreshToken }, ct);

    public static Task<HttpResponseMessage> SignOutWithBodyAsync(this HttpClient client, string? refreshToken, CancellationToken ct) =>
        client.PostAsJsonAsync("/api/v1/auth/sign-out", new { refreshToken }, ct);

    public static async Task<string> RefreshTokenAsync(this HttpResponseMessage response, CancellationToken ct) =>
        (await response.ReadJsonAsync(ct)).GetProperty("refreshToken").GetString()!;

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string? cookieValue, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, RefreshPath);
        if (cookieValue is not null)
        {
            request.Headers.Add("Cookie", $"{CookieName}={cookieValue}");
        }

        return client.SendAsync(request, ct);
    }

    public static Task<HttpResponseMessage> GetAsync(this HttpClient client, string path, string? bearer, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (bearer is not null)
        {
            request.Headers.Add("Authorization", "Bearer " + bearer);
        }

        return client.SendAsync(request, ct);
    }

    // Read as text, so the same response can be read more than once in a test.
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response, CancellationToken ct) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(ct));

    public static async Task<string> AccessTokenAsync(this HttpResponseMessage response, CancellationToken ct) =>
        (await response.ReadJsonAsync(ct)).GetProperty("accessToken").GetString()!;

    // The whole Set-Cookie header of the refresh cookie, or null when the response does not set it.
    public static string? RefreshSetCookie(this HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(CookieName + "=", StringComparison.Ordinal))
            : null;

    // Just the secret inside it.
    public static string? RefreshCookieValue(this HttpResponseMessage response) =>
        response.RefreshSetCookie()?[(CookieName.Length + 1)..].Split(';')[0];

    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response, CancellationToken ct) =>
        (await response.ReadJsonAsync(ct)).GetProperty("code").GetString();

    public static JsonWebToken Read(string accessToken) => new JsonWebTokenHandler().ReadJsonWebToken(accessToken);

    // A token for a role, always obtained by really signing in. "Driver" is a crew PHONE token (no cab session);
    // "DriverCab" is a cab session token from CAB-01 and DRV-01 (it ends any earlier cab session of that driver or tablet, R-19).
    public static async Task<string> TokenForAsync(this ApiFixture api, string role, CancellationToken ct)
    {
        var client = api.Factory.NewClient();
        switch (role)
        {
            case "Controller":
                return await (await client.SignInAsync("CTRL-01", api.DemoPassword, ct)).AccessTokenAsync(ct);
            case "StationManager":
                return await (await client.SignInAsync("SM-FOT", api.DemoPassword, ct)).AccessTokenAsync(ct);
            case "Driver":
                return await (await client.CrewSignInAsync("DRV-01", api.DemoPassword, ct)).AccessTokenAsync(ct);
            case "DriverCab":
                return await (await client.CabSignInAsync("CAB-01", api.CabDeviceSecrets["CAB-01"], "DRV-01", api.DemoPassword, api.Clock.GetUtcNow(), ct)).AccessTokenAsync(ct);
            default:
                throw new ArgumentException("No token for role " + role);
        }
    }
}
