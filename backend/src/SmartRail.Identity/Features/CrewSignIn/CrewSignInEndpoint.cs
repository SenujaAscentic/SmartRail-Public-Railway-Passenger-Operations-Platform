using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using SmartRail.Identity.Authentication;
using SmartRail.Identity.Model;
using SmartRail.Identity.Persistence;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity.Features.CrewSignIn;

// POST /api/v1/auth/crew/sign-in   (contract 3.4, policy Public, US-C1)
internal static class CrewSignInEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapPost("/auth/crew/sign-in", HandleAsync)
            .AllowAnonymous()
            .WithName("CrewSignIn")
            .WithTags("Identity")
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> HandleAsync(
        PasswordSignInRequest request,
        HttpContext http,
        UserManager<StaffAccount> users,
        IPasswordHasher<StaffAccount> passwordHasher,
        IdentityModuleDbContext db,
        AccessTokenIssuer issuer,
        TimeProvider clock,
        CancellationToken ct)
    {
        var errors = PasswordSignInValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Problems.Validation(http, errors);
        }

        return await CrewSignInHandler.HandleAsync(request, http, users, passwordHasher, db, issuer, clock, ct);
    }
}
