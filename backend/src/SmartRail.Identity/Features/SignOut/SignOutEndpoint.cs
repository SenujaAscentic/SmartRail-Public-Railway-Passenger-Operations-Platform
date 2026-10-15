using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SmartRail.Identity.Features.SignOut;

// POST /api/v1/auth/sign-out   (contract 3.2)
// Public in the policy sense, like refresh: the credential is the refresh cookie. The access token is not revoked
// (it simply expires within 15 minutes); the app discards it.
internal static class SignOutEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapPost("/auth/sign-out", SignOutHandler.HandleAsync)
            .AllowAnonymous()
            .WithName("SignOut")
            .WithTags("Identity")
            .Produces(StatusCodes.Status204NoContent);
}
