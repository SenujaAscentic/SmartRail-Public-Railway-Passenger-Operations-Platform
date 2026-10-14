using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity.Features.RefreshSession;

// POST /api/v1/auth/refresh   (contract 3.2, ADR-10)
// Public in the policy sense: the credential is the refresh cookie, not a bearer token (the access token has usually expired by now).
internal static class RefreshSessionEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapPost("/auth/refresh", RefreshSessionHandler.HandleAsync)
            .AllowAnonymous()
            .WithName("RefreshSession")
            .WithTags("Identity")
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
}
