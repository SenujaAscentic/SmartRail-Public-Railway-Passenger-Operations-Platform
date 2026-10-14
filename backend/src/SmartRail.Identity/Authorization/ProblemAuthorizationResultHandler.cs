using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace SmartRail.Identity.Authorization;

// Decides what the client sees when a policy refuses a request: 401 (not signed in) or 403 (wrong role),
// both as Problem Details with a stable code (contract section 6). The framework's default sends an empty body.
internal sealed class ProblemAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await Problems.Unauthenticated(context).ExecuteAsync(context);
            return;
        }

        if (authorizeResult.Forbidden)
        {
            await Problems.Forbidden(context).ExecuteAsync(context);
            return;
        }

        await next(context);
    }
}
