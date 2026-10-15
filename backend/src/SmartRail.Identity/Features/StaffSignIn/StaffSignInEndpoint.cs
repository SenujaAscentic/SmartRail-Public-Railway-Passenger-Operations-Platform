using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using SmartRail.Identity.Model;
using SmartRail.Identity.Persistence;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity.Features.StaffSignIn;

// POST /api/v1/auth/staff/sign-in   (contract 3.3, policy Public, US-O1)
internal static class StaffSignInEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapPost("/auth/staff/sign-in", HandleAsync)
            .AllowAnonymous()
            .WithName("StaffSignIn")
            .WithTags("Identity")
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> HandleAsync(
        StaffSignInRequest request,
        HttpContext http,
        UserManager<StaffAccount> users,
        IPasswordHasher<StaffAccount> passwordHasher,
        IdentityModuleDbContext db,
        AccessTokenIssuer issuer,
        TimeProvider clock,
        CancellationToken ct)
    {
        var errors = StaffSignInValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Problems.Validation(http, errors);
        }

        return await StaffSignInHandler.HandleAsync(request, http, users, passwordHasher, db, issuer, clock, ct);
    }
}
