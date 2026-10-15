using System.Net;
using Xunit;

namespace SmartRail.IntegrationTests.Identity;

// POST /api/v1/auth/sign-out   (contract 3.2, US-O1, threat T-14)
[Collection("Api")]
public class SignOutTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string SignOutPath = "/api/v1/auth/sign-out";

    private readonly HttpClient _client = api.Factory.NewClient();

    private Task<HttpResponseMessage> SignOutAsync(string? cookieValue)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, SignOutPath);
        if (cookieValue is not null)
        {
            request.Headers.Add("Cookie", $"{AuthTestHelpers.CookieName}={cookieValue}");
        }

        return _client.SendAsync(request, Ct);
    }

    private async Task<string> SignInAndGetCookie(string staffNumber = "CTRL-01") =>
        (await _client.SignInAsync(staffNumber, api.DemoPassword, Ct)).RefreshCookieValue()!;

    [Fact]
    public async Task After_signing_out_the_refresh_token_no_longer_works()
    {
        var cookie = await SignInAndGetCookie();
        Assert.Equal(HttpStatusCode.NoContent, (await SignOutAsync(cookie)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.RefreshAsync(cookie, Ct)).StatusCode);
    }

    [Fact]
    public async Task Signing_out_ends_the_whole_session_including_older_tokens_inside_the_grace_window()
    {
        var first = await SignInAndGetCookie();
        var second = (await _client.RefreshAsync(first, Ct)).RefreshCookieValue()!;

        await SignOutAsync(second);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.RefreshAsync(second, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.RefreshAsync(first, Ct)).StatusCode);   // would be a valid replay if not revoked
    }

    [Fact]
    public async Task The_answer_clears_the_cookie()
    {
        var cookie = await SignInAndGetCookie();

        var response = await SignOutAsync(cookie);

        var cleared = response.RefreshSetCookie();
        Assert.NotNull(cleared);
        Assert.Equal("", response.RefreshCookieValue());   // empty value
        Assert.Contains("expires=Thu, 01 Jan 1970", cleared, StringComparison.OrdinalIgnoreCase);   // already expired: the browser deletes it
        Assert.Contains("path=/api/v1/auth", cleared, StringComparison.OrdinalIgnoreCase);        // same path, or the browser keeps the old cookie
    }

    [Fact]
    public async Task Another_session_of_the_same_person_stays_signed_in()
    {
        var office = await SignInAndGetCookie("SM-FOT");
        var laptop = await SignInAndGetCookie("SM-FOT");

        await SignOutAsync(office);

        Assert.Equal(HttpStatusCode.OK, (await _client.RefreshAsync(laptop, Ct)).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("this-was-never-issued")]
    public async Task Signing_out_without_a_real_session_is_harmless_and_still_answers_204(string? cookie)
    {
        var response = await SignOutAsync(cookie);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
