using System.Net;
using Xunit;

namespace SmartRail.IntegrationTests;

// Every endpoint, every role, expected allow / deny (CLAUDE.md section 5).
// Sign-in and tokens do not exist yet, so only the anonymous caller can be tested. When sign-in is built,
// add one row per role (Passenger, Controller, StationManager, Driver, Guard, Display, Provider) to each endpoint below.
[Collection("Api")]
public class PermissionMatrixTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // role, method, path, expected status
    public static TheoryData<string, string, string, HttpStatusCode> Rows => new()
    {
        { "Anonymous", "GET", "/api/v1/stations?query=kan", HttpStatusCode.OK },   // Public
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task Endpoint_answers_each_role_as_the_contract_says(string role, string method, string path, HttpStatusCode expected)
    {
        Assert.Equal("Anonymous", role);   // fails loudly if someone adds a signed-in role row before sign-in can be sent

        var client = api.Factory.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path), Ct);

        Assert.Equal(expected, response.StatusCode);
    }
}
