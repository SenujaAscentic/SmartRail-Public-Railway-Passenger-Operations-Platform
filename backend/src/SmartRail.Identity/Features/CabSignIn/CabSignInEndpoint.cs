using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SmartRail.Identity.Model;
using SmartRail.Identity.Persistence;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity.Features.CabSignIn;

// POST /api/v1/auth/crew/cab-sign-in   (contract 3.4, policy Public: the device credential in the headers and the password are the credentials, US-C2)
internal static class CabSignInEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapPost("/auth/crew/cab-sign-in", HandleAsync)
            .AllowAnonymous()
            .WithName("CabSignIn")
            .WithTags("Identity")
            .Produces<CabSignInResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> HandleAsync(
        [FromHeader(Name = CabSignInValidator.DeviceIdHeader)] string? deviceId,
        [FromHeader(Name = CabSignInValidator.DeviceSecretHeader)] string? deviceSecret,
        CabSignInRequest request,
        HttpContext http,
        UserManager<StaffAccount> users,
        IPasswordHasher<StaffAccount> passwordHasher,
        IdentityModuleDbContext db,
        AccessTokenIssuer issuer,
        TimeProvider clock,
        CancellationToken ct)
    {
        var errors = CabSignInValidator.Validate(deviceId, deviceSecret, request, clock.GetUtcNow());
        if (errors.Count > 0)
        {
            return Problems.Validation(http, errors);
        }

        return await CabSignInHandler.HandleAsync(deviceId!, deviceSecret!, request, http, users, passwordHasher, db, issuer, clock, ct);
    }
}
