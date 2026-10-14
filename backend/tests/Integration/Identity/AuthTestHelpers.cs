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

    // The refresh cookie is Secure, and the test server is plain http. Cookies are therefore handled by hand
    // (read from Set-Cookie, sent in a Cookie header) so the tests control exactly what the browser would send.
    public static HttpClient NewClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    public static Task<HttpResponseMessage> SignInAsync(this HttpClient client, string staffNumber, string password, CancellationToken ct) =>
        client.PostAsJsonAsync(SignInPath, new { staffNumber, password }, ct);

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

    // A token for a role. Controller and StationManager really sign in; a Driver cannot sign in on the staff endpoint
    // (crew sign-in is the next task), so the driver token is minted with the application's own issuer.
    public static async Task<string> TokenForAsync(this ApiFixture api, string role, CancellationToken ct)
    {
        switch (role)
        {
            case "Controller":
                return await (await api.Factory.NewClient().SignInAsync("CTRL-01", api.DemoPassword, ct)).AccessTokenAsync(ct);
            case "StationManager":
                return await (await api.Factory.NewClient().SignInAsync("SM-FOT", api.DemoPassword, ct)).AccessTokenAsync(ct);
            case "Driver":
                return api.Factory.Services.GetRequiredService<AccessTokenIssuer>().Issue(Guid.NewGuid(), StaffRole.Driver, stationId: null).Token;
            default:
                throw new ArgumentException("No token for role " + role);
        }
    }
}
