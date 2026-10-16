using System.Net;
using System.Text;
using SmartRail.IntegrationTests.Identity;
using Xunit;

namespace SmartRail.IntegrationTests;

// Every endpoint, every role, expected allow / deny (CLAUDE.md section 5, threat T-04).
// Roles that exist so far: Anonymous, Controller, StationManager, Driver (a crew PHONE token), DriverCab (a cab session token).
// Add Passenger, Display and Provider rows when their credentials exist. The policies themselves are checked in PolicyTests.
[Collection("Api")]
public class PermissionMatrixTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // role, method, path, expected status
    public static TheoryData<string, string, string, HttpStatusCode> Rows => new()
    {
        // GET /stations: Public.
        { "Anonymous", "GET", "/api/v1/stations?query=kan", HttpStatusCode.OK },
        { "Controller", "GET", "/api/v1/stations?query=kan", HttpStatusCode.OK },
        { "StationManager", "GET", "/api/v1/stations?query=kan", HttpStatusCode.OK },
        { "Driver", "GET", "/api/v1/stations?query=kan", HttpStatusCode.OK },
        { "DriverCab", "GET", "/api/v1/stations?query=kan", HttpStatusCode.OK },

        // The API description: Public.
        { "Anonymous", "GET", "/openapi/v1.yaml", HttpStatusCode.OK },

        // POST /auth/staff/sign-in: Public. An empty body is answered 400, which shows the request got past the policy.
        { "Anonymous", "POST", "/api/v1/auth/staff/sign-in", HttpStatusCode.BadRequest },
        { "Controller", "POST", "/api/v1/auth/staff/sign-in", HttpStatusCode.BadRequest },
        { "Driver", "POST", "/api/v1/auth/staff/sign-in", HttpStatusCode.BadRequest },
        { "DriverCab", "POST", "/api/v1/auth/staff/sign-in", HttpStatusCode.BadRequest },

        // POST /auth/crew/sign-in and /auth/crew/cab-sign-in: Public; the password (and for the tablet the device credential) is the credential.
        // An empty body (and no device headers) is answered 400, which shows the request got past the policy.
        { "Anonymous", "POST", "/api/v1/auth/crew/sign-in", HttpStatusCode.BadRequest },
        { "Controller", "POST", "/api/v1/auth/crew/sign-in", HttpStatusCode.BadRequest },
        { "StationManager", "POST", "/api/v1/auth/crew/sign-in", HttpStatusCode.BadRequest },
        { "Driver", "POST", "/api/v1/auth/crew/sign-in", HttpStatusCode.BadRequest },
        { "DriverCab", "POST", "/api/v1/auth/crew/sign-in", HttpStatusCode.BadRequest },
        { "Anonymous", "POST", "/api/v1/auth/crew/cab-sign-in", HttpStatusCode.BadRequest },
        { "Controller", "POST", "/api/v1/auth/crew/cab-sign-in", HttpStatusCode.BadRequest },
        { "StationManager", "POST", "/api/v1/auth/crew/cab-sign-in", HttpStatusCode.BadRequest },
        { "Driver", "POST", "/api/v1/auth/crew/cab-sign-in", HttpStatusCode.BadRequest },
        { "DriverCab", "POST", "/api/v1/auth/crew/cab-sign-in", HttpStatusCode.BadRequest },

        // POST /auth/refresh: Public in the policy sense; the refresh cookie is the credential, so a bearer token changes nothing.
        { "Anonymous", "POST", "/api/v1/auth/refresh", HttpStatusCode.Unauthorized },
        { "Controller", "POST", "/api/v1/auth/refresh", HttpStatusCode.Unauthorized },
        { "Driver", "POST", "/api/v1/auth/refresh", HttpStatusCode.Unauthorized },
        { "DriverCab", "POST", "/api/v1/auth/refresh", HttpStatusCode.Unauthorized },

        // POST /auth/sign-out: Public in the policy sense, like refresh: the cookie is the credential. Always 204, even with no session.
        { "Anonymous", "POST", "/api/v1/auth/sign-out", HttpStatusCode.NoContent },
        { "Controller", "POST", "/api/v1/auth/sign-out", HttpStatusCode.NoContent },
        { "StationManager", "POST", "/api/v1/auth/sign-out", HttpStatusCode.NoContent },
        { "Driver", "POST", "/api/v1/auth/sign-out", HttpStatusCode.NoContent },
        { "DriverCab", "POST", "/api/v1/auth/sign-out", HttpStatusCode.NoContent },

        // GET /staff/me: policy Staff (Controller, StationManager).
        { "Anonymous", "GET", "/api/v1/staff/me", HttpStatusCode.Unauthorized },
        { "Controller", "GET", "/api/v1/staff/me", HttpStatusCode.OK },
        { "StationManager", "GET", "/api/v1/staff/me", HttpStatusCode.OK },
        { "Driver", "GET", "/api/v1/staff/me", HttpStatusCode.Forbidden },
        { "DriverCab", "GET", "/api/v1/staff/me", HttpStatusCode.Forbidden },

        // GET /stream: Public. Default-deny is on, so the endpoint must say AllowAnonymous itself: Anonymous getting 200 proves it.
        // With a filter (a journey that does not exist: nothing to show yet) the stream opens for every role.
        // Without any filter every role gets 400 (past the policy, refused by validation).
        { "Anonymous", "GET", "/api/v1/stream?journeyIds=00000000-0000-0000-0000-000000000001", HttpStatusCode.OK },
        { "Controller", "GET", "/api/v1/stream?journeyIds=00000000-0000-0000-0000-000000000001", HttpStatusCode.OK },
        { "StationManager", "GET", "/api/v1/stream?journeyIds=00000000-0000-0000-0000-000000000001", HttpStatusCode.OK },
        { "Driver", "GET", "/api/v1/stream?journeyIds=00000000-0000-0000-0000-000000000001", HttpStatusCode.OK },
        { "DriverCab", "GET", "/api/v1/stream?journeyIds=00000000-0000-0000-0000-000000000001", HttpStatusCode.OK },
        { "Anonymous", "GET", "/api/v1/stream", HttpStatusCode.BadRequest },
        { "Controller", "GET", "/api/v1/stream", HttpStatusCode.BadRequest },
        { "StationManager", "GET", "/api/v1/stream", HttpStatusCode.BadRequest },
        { "Driver", "GET", "/api/v1/stream", HttpStatusCode.BadRequest },
        { "DriverCab", "GET", "/api/v1/stream", HttpStatusCode.BadRequest },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task Endpoint_answers_each_role_as_the_contract_says(string role, string method, string path, HttpStatusCode expected)
    {
        var client = api.Factory.NewClient();
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (role != "Anonymous")
        {
            request.Headers.Add("Authorization", "Bearer " + await api.TokenForAsync(role, Ct));
        }

        if (method == "POST")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        // Headers only: an open event stream never ends, so the body must not be waited for. Disposing ends the request.
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, Ct);

        Assert.Equal(expected, response.StatusCode);
    }
}
