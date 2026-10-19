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

        // GET /lines and GET /stations/{id}: Public. An unknown station is 404 for every role (never 401 or 403: the endpoint is public).
        { "Anonymous", "GET", "/api/v1/lines", HttpStatusCode.OK },
        { "Controller", "GET", "/api/v1/lines", HttpStatusCode.OK },
        { "StationManager", "GET", "/api/v1/lines", HttpStatusCode.OK },
        { "Driver", "GET", "/api/v1/lines", HttpStatusCode.OK },
        { "DriverCab", "GET", "/api/v1/lines", HttpStatusCode.OK },
        { "Anonymous", "GET", "/api/v1/stations/00000000-0000-0000-0000-000000000001", HttpStatusCode.NotFound },
        { "Controller", "GET", "/api/v1/stations/00000000-0000-0000-0000-000000000001", HttpStatusCode.NotFound },
        { "StationManager", "GET", "/api/v1/stations/00000000-0000-0000-0000-000000000001", HttpStatusCode.NotFound },
        { "Driver", "GET", "/api/v1/stations/00000000-0000-0000-0000-000000000001", HttpStatusCode.NotFound },
        { "DriverCab", "GET", "/api/v1/stations/00000000-0000-0000-0000-000000000001", HttpStatusCode.NotFound },

        // GET /journeys/search and GET /journeys/{id}: Public. No parameters is 400 and an unknown journey is 404 for every role
        // (never 401 or 403: the endpoints are public, so a request that gets past the policy shows it).
        { "Anonymous", "GET", "/api/v1/journeys/search", HttpStatusCode.BadRequest },
        { "Controller", "GET", "/api/v1/journeys/search", HttpStatusCode.BadRequest },
        { "StationManager", "GET", "/api/v1/journeys/search", HttpStatusCode.BadRequest },
        { "Driver", "GET", "/api/v1/journeys/search", HttpStatusCode.BadRequest },
        { "DriverCab", "GET", "/api/v1/journeys/search", HttpStatusCode.BadRequest },
        { "Anonymous", "GET", "/api/v1/journeys/00000000-0000-0000-0000-000000000001", HttpStatusCode.NotFound },
        { "Controller", "GET", "/api/v1/journeys/00000000-0000-0000-0000-000000000001", HttpStatusCode.NotFound },
        { "StationManager", "GET", "/api/v1/journeys/00000000-0000-0000-0000-000000000001", HttpStatusCode.NotFound },
        { "Driver", "GET", "/api/v1/journeys/00000000-0000-0000-0000-000000000001", HttpStatusCode.NotFound },
        { "DriverCab", "GET", "/api/v1/journeys/00000000-0000-0000-0000-000000000001", HttpStatusCode.NotFound },

        // POST /crew/journeys/{id}/start and /end: CrewCab only (a driver with a cab session). The test posts an empty body, so a
        // DriverCab caller gets past the policy and is answered 400 (no clientActionId). A phone token (Driver) is refused, so is staff.
        { "Anonymous", "POST", "/api/v1/crew/journeys/00000000-0000-0000-0000-000000000001/start", HttpStatusCode.Unauthorized },
        { "Controller", "POST", "/api/v1/crew/journeys/00000000-0000-0000-0000-000000000001/start", HttpStatusCode.Forbidden },
        { "StationManager", "POST", "/api/v1/crew/journeys/00000000-0000-0000-0000-000000000001/start", HttpStatusCode.Forbidden },
        { "Driver", "POST", "/api/v1/crew/journeys/00000000-0000-0000-0000-000000000001/start", HttpStatusCode.Forbidden },
        { "DriverCab", "POST", "/api/v1/crew/journeys/00000000-0000-0000-0000-000000000001/start", HttpStatusCode.BadRequest },
        { "Anonymous", "POST", "/api/v1/crew/journeys/00000000-0000-0000-0000-000000000001/end", HttpStatusCode.Unauthorized },
        { "Controller", "POST", "/api/v1/crew/journeys/00000000-0000-0000-0000-000000000001/end", HttpStatusCode.Forbidden },
        { "StationManager", "POST", "/api/v1/crew/journeys/00000000-0000-0000-0000-000000000001/end", HttpStatusCode.Forbidden },
        { "Driver", "POST", "/api/v1/crew/journeys/00000000-0000-0000-0000-000000000001/end", HttpStatusCode.Forbidden },
        { "DriverCab", "POST", "/api/v1/crew/journeys/00000000-0000-0000-0000-000000000001/end", HttpStatusCode.BadRequest },

        // POST /crew/journeys/{id}/positions: CrewCab only (R-07, threat T-19). Empty body: a DriverCab caller gets past the policy and is
        // answered 400; a phone token (Driver), staff and nobody are refused. Who may post for WHICH journey is in CrewPositionsR07Tests.
        { "Anonymous", "POST", "/api/v1/crew/journeys/00000000-0000-0000-0000-000000000001/positions", HttpStatusCode.Unauthorized },
        { "Controller", "POST", "/api/v1/crew/journeys/00000000-0000-0000-0000-000000000001/positions", HttpStatusCode.Forbidden },
        { "StationManager", "POST", "/api/v1/crew/journeys/00000000-0000-0000-0000-000000000001/positions", HttpStatusCode.Forbidden },
        { "Driver", "POST", "/api/v1/crew/journeys/00000000-0000-0000-0000-000000000001/positions", HttpStatusCode.Forbidden },
        { "DriverCab", "POST", "/api/v1/crew/journeys/00000000-0000-0000-0000-000000000001/positions", HttpStatusCode.BadRequest },

        // POST and DELETE /ops/journeys/{id}/delay: Controller only (policy Controller). The empty body makes the Controller's request a 400, so it is past the policy; every other role is refused.
        { "Anonymous", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/delay", HttpStatusCode.Unauthorized },
        { "Controller", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/delay", HttpStatusCode.BadRequest },
        { "StationManager", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/delay", HttpStatusCode.Forbidden },
        { "Driver", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/delay", HttpStatusCode.Forbidden },
        { "DriverCab", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/delay", HttpStatusCode.Forbidden },
        { "Anonymous", "DELETE", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/delay", HttpStatusCode.Unauthorized },
        { "Controller", "DELETE", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/delay", HttpStatusCode.BadRequest },
        { "StationManager", "DELETE", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/delay", HttpStatusCode.Forbidden },
        { "Driver", "DELETE", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/delay", HttpStatusCode.Forbidden },
        { "DriverCab", "DELETE", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/delay", HttpStatusCode.Forbidden },

        // POST /ops/journeys/{id}/hold and /hold/release: Controller only.
        { "Anonymous", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/hold", HttpStatusCode.Unauthorized },
        { "Controller", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/hold", HttpStatusCode.BadRequest },
        { "StationManager", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/hold", HttpStatusCode.Forbidden },
        { "Driver", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/hold", HttpStatusCode.Forbidden },
        { "DriverCab", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/hold", HttpStatusCode.Forbidden },
        { "Anonymous", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/hold/release", HttpStatusCode.Unauthorized },
        { "Controller", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/hold/release", HttpStatusCode.BadRequest },
        { "StationManager", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/hold/release", HttpStatusCode.Forbidden },
        { "Driver", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/hold/release", HttpStatusCode.Forbidden },
        { "DriverCab", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/hold/release", HttpStatusCode.Forbidden },

        // POST /ops/journeys/{id}/cancel and /reinstate: Controller only.
        { "Anonymous", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/cancel", HttpStatusCode.Unauthorized },
        { "Controller", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/cancel", HttpStatusCode.BadRequest },
        { "StationManager", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/cancel", HttpStatusCode.Forbidden },
        { "Driver", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/cancel", HttpStatusCode.Forbidden },
        { "DriverCab", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/cancel", HttpStatusCode.Forbidden },
        { "Anonymous", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/reinstate", HttpStatusCode.Unauthorized },
        { "Controller", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/reinstate", HttpStatusCode.BadRequest },
        { "StationManager", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/reinstate", HttpStatusCode.Forbidden },
        { "Driver", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/reinstate", HttpStatusCode.Forbidden },
        { "DriverCab", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/reinstate", HttpStatusCode.Forbidden },

        // POST /ops/journeys/{id}/stops/{stationId}/platform and /platform/preview: Controller only.
        { "Anonymous", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/stops/00000000-0000-0000-0000-000000000002/platform", HttpStatusCode.Unauthorized },
        { "Controller", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/stops/00000000-0000-0000-0000-000000000002/platform", HttpStatusCode.BadRequest },
        { "StationManager", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/stops/00000000-0000-0000-0000-000000000002/platform", HttpStatusCode.Forbidden },
        { "Driver", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/stops/00000000-0000-0000-0000-000000000002/platform", HttpStatusCode.Forbidden },
        { "DriverCab", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/stops/00000000-0000-0000-0000-000000000002/platform", HttpStatusCode.Forbidden },
        { "Anonymous", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/stops/00000000-0000-0000-0000-000000000002/platform/preview", HttpStatusCode.Unauthorized },
        { "Controller", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/stops/00000000-0000-0000-0000-000000000002/platform/preview", HttpStatusCode.BadRequest },
        { "StationManager", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/stops/00000000-0000-0000-0000-000000000002/platform/preview", HttpStatusCode.Forbidden },
        { "Driver", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/stops/00000000-0000-0000-0000-000000000002/platform/preview", HttpStatusCode.Forbidden },
        { "DriverCab", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/stops/00000000-0000-0000-0000-000000000002/platform/preview", HttpStatusCode.Forbidden },

        // POST /ops/journeys/{id}/terminate: Controller only.
        { "Anonymous", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/terminate", HttpStatusCode.Unauthorized },
        { "Controller", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/terminate", HttpStatusCode.BadRequest },
        { "StationManager", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/terminate", HttpStatusCode.Forbidden },
        { "Driver", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/terminate", HttpStatusCode.Forbidden },
        { "DriverCab", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/terminate", HttpStatusCode.Forbidden },

        // POST /ops/journeys/{id}/stops/{stationId}/skip: Controller only.
        { "Anonymous", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/stops/00000000-0000-0000-0000-000000000002/skip", HttpStatusCode.Unauthorized },
        { "Controller", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/stops/00000000-0000-0000-0000-000000000002/skip", HttpStatusCode.BadRequest },
        { "StationManager", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/stops/00000000-0000-0000-0000-000000000002/skip", HttpStatusCode.Forbidden },
        { "Driver", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/stops/00000000-0000-0000-0000-000000000002/skip", HttpStatusCode.Forbidden },
        { "DriverCab", "POST", "/api/v1/ops/journeys/00000000-0000-0000-0000-000000000001/stops/00000000-0000-0000-0000-000000000002/skip", HttpStatusCode.Forbidden },

        // GET /ops/stream: policy Staff (Controller, StationManager). Headers only: the stream never ends by itself.
        { "Anonymous", "GET", "/api/v1/ops/stream?journeyIds=00000000-0000-0000-0000-000000000001", HttpStatusCode.Unauthorized },
        { "Controller", "GET", "/api/v1/ops/stream?journeyIds=00000000-0000-0000-0000-000000000001", HttpStatusCode.OK },
        { "StationManager", "GET", "/api/v1/ops/stream?journeyIds=00000000-0000-0000-0000-000000000001", HttpStatusCode.OK },
        { "Driver", "GET", "/api/v1/ops/stream?journeyIds=00000000-0000-0000-0000-000000000001", HttpStatusCode.Forbidden },
        { "DriverCab", "GET", "/api/v1/ops/stream?journeyIds=00000000-0000-0000-0000-000000000001", HttpStatusCode.Forbidden },

        // GET /stations/{id}/catering: Public (an unknown station is a 404 for everyone). A display token has no credential type yet; it reads
        // the public endpoints like Anonymous, and the catering route has no write method (a POST is 401 without a token, 405 with one).
        { "Anonymous", "GET", "/api/v1/stations/00000000-0000-0000-0000-000000000001/catering", HttpStatusCode.NotFound },
        { "Controller", "GET", "/api/v1/stations/00000000-0000-0000-0000-000000000001/catering", HttpStatusCode.NotFound },
        { "StationManager", "GET", "/api/v1/stations/00000000-0000-0000-0000-000000000001/catering", HttpStatusCode.NotFound },
        { "Driver", "GET", "/api/v1/stations/00000000-0000-0000-0000-000000000001/catering", HttpStatusCode.NotFound },
        { "DriverCab", "GET", "/api/v1/stations/00000000-0000-0000-0000-000000000001/catering", HttpStatusCode.NotFound },
        { "Anonymous", "POST", "/api/v1/stations/00000000-0000-0000-0000-000000000001/catering", HttpStatusCode.Unauthorized },
        { "Controller", "POST", "/api/v1/stations/00000000-0000-0000-0000-000000000001/catering", HttpStatusCode.MethodNotAllowed },

        // GET /stations/{id}/board: Public (an unknown station is a 404 for everyone).
        { "Anonymous", "GET", "/api/v1/stations/00000000-0000-0000-0000-000000000001/board", HttpStatusCode.NotFound },
        { "Controller", "GET", "/api/v1/stations/00000000-0000-0000-0000-000000000001/board", HttpStatusCode.NotFound },
        { "StationManager", "GET", "/api/v1/stations/00000000-0000-0000-0000-000000000001/board", HttpStatusCode.NotFound },
        { "Driver", "GET", "/api/v1/stations/00000000-0000-0000-0000-000000000001/board", HttpStatusCode.NotFound },
        { "DriverCab", "GET", "/api/v1/stations/00000000-0000-0000-0000-000000000001/board", HttpStatusCode.NotFound },

        // GET /ops/audit: Controller only (the audit trail).
        { "Anonymous", "GET", "/api/v1/ops/audit", HttpStatusCode.Unauthorized },
        { "Controller", "GET", "/api/v1/ops/audit", HttpStatusCode.OK },
        { "StationManager", "GET", "/api/v1/ops/audit", HttpStatusCode.Forbidden },
        { "Driver", "GET", "/api/v1/ops/audit", HttpStatusCode.Forbidden },
        { "DriverCab", "GET", "/api/v1/ops/audit", HttpStatusCode.Forbidden },
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
