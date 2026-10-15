using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity.Features.SignOut;

// POST /api/v1/auth/sign-out   (contract 3.2)
// Public in the policy sense, like refresh: the credential is the refresh token (cookie for staff, body for crew apps).
// The access token is not revoked (it simply expires within 15 minutes; a cab token stops at once because its session ends).
internal static class SignOutEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapPost("/auth/sign-out", SignOutHandler.HandleAsync)
            .AllowAnonymous()
            .WithName("SignOut")
            .WithTags("Identity")
            .Accepts<RefreshRequest>(isOptional: true, "application/json")
            .Produces(StatusCodes.Status204NoContent);
}
