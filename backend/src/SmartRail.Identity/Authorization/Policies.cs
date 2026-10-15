using Microsoft.AspNetCore.Authorization;

namespace SmartRail.Identity.Authorization;

// The policy names of contract section 2. Other modules write the same names as text in their endpoints
// (for example RequireAuthorization("Controller")); a module may not reference Identity's code.
// CrewCab (cab session), Display, Provider and Passenger are added with the credentials they belong to.
internal static class Policies
{
    public const string Controller = "Controller";
    public const string StationManager = "StationManager";
    public const string Staff = "Staff";            // ops-web staff: a controller or a station manager
    public const string CrewPhone = "CrewPhone";    // a driver on a phone or on a tablet (the guard role is deferred)
    public const string CrewCab = "CrewCab";        // a driver with a CAB SESSION: the token carries crewSessionId (phone tokens do not)

    public static void Register(AuthorizationOptions options)
    {
        // Secure by default: an endpoint that names no policy still needs a signed-in caller.
        // Public endpoints say so explicitly with AllowAnonymous().
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

        // The "role" claim of the access token holds the role name.
        options.AddPolicy(Controller, policy => policy.RequireRole("Controller"));
        options.AddPolicy(StationManager, policy => policy.RequireRole("StationManager"));
        options.AddPolicy(Staff, policy => policy.RequireRole("Controller", "StationManager"));
        options.AddPolicy(CrewPhone, policy => policy.RequireRole("Driver"));
        options.AddPolicy(CrewCab, policy => policy.RequireRole("Driver").RequireClaim("crewSessionId"));
    }
}
