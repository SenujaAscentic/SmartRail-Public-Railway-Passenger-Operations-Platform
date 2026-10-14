using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SmartRail.IntegrationTests.Identity;

// The policies of contract section 2, role by role. No ops or crew endpoints exist yet to carry them, so the policies
// are asked directly. When an endpoint uses a policy, it also gets rows in PermissionMatrixTests.
[Collection("Api")]
public class PolicyTests(ApiFixture api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // role, policy, allowed
    public static TheoryData<string, string, bool> Rows => new()
    {
        { "Anonymous", "Controller", false }, { "Anonymous", "StationManager", false }, { "Anonymous", "Staff", false }, { "Anonymous", "CrewPhone", false },
        { "Controller", "Controller", true }, { "Controller", "StationManager", false }, { "Controller", "Staff", true }, { "Controller", "CrewPhone", false },
        { "StationManager", "Controller", false }, { "StationManager", "StationManager", true }, { "StationManager", "Staff", true }, { "StationManager", "CrewPhone", false },
        { "Driver", "Controller", false }, { "Driver", "StationManager", false }, { "Driver", "Staff", false }, { "Driver", "CrewPhone", true },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task Each_policy_allows_exactly_the_roles_of_the_contract(string role, string policy, bool expected)
    {
        var principal = role == "Anonymous"
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", role)], "test", nameType: "sub", roleType: "role"));
        var authorization = api.Factory.Services.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(principal, resource: null, policy);

        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public void Every_endpoint_states_who_may_call_it()
    {
        var endpoints = api.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        var undeclared = endpoints
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null && e.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(e => e.RoutePattern.RawText)
            .ToList();

        Assert.True(undeclared.Count == 0, "Endpoints with no AllowAnonymous() and no policy: " + string.Join(", ", undeclared));
    }
}
