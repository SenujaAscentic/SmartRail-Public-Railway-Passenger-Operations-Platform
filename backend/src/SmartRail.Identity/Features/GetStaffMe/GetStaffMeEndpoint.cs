using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using SmartRail.Identity.Authorization;
using SmartRail.Identity.Model;

namespace SmartRail.Identity.Features.GetStaffMe;

// The signed-in staff member as the ops web shows it: name, role and scope, so the UI can choose its defaults.
// stationId is the station of a station manager; null means the whole line (the controller).
public sealed record StaffMeResponse(string Name, string Role, Guid? StationId);

// GET /api/v1/staff/me   (contract 3.3, policy Staff, US-O1)
internal static class GetStaffMeEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapGet("/staff/me", HandleAsync)
            .RequireAuthorization(Policies.Staff)
            .WithName("GetStaffMe")
            .WithTags("Identity")
            .Produces<StaffMeResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

    private static async Task<IResult> HandleAsync(ClaimsPrincipal caller, HttpContext http, UserManager<StaffAccount> users)
    {
        // The account comes from the token's "sub", never from the URL or the body.
        var account = await users.FindByIdAsync(caller.FindFirstValue("sub") ?? "");

        // A token can outlive its account's activation for up to 15 minutes; a deactivated account gets nothing.
        if (account is null || !account.IsActive)
        {
            return Problems.Unauthenticated(http);
        }

        return Results.Ok(new StaffMeResponse(account.Name, account.Role.ToString(), account.StationId));
    }
}
