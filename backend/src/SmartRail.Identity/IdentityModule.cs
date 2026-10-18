using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SmartRail.Identity.Authentication;
using SmartRail.Identity.Authorization;
using SmartRail.Identity.Features.CabSignIn;
using SmartRail.Identity.Features.CrewSignIn;
using SmartRail.Identity.Features.GetStaffMe;
using SmartRail.Identity.Features.RefreshSession;
using SmartRail.Identity.Features.SignOut;
using SmartRail.Identity.Features.StaffSignIn;
using SmartRail.Identity.Model;
using SmartRail.Identity.Persistence;
using SmartRail.Identity.Seed;
using SmartRail.Identity.Tokens;

namespace SmartRail.Identity;

// The Identity module's entry points for the host (SmartRail.Api).
public static class IdentityModule
{
    // 5 wrong passwords lock the account for this long (threat T-01, contract section 1).
    internal static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // Call this AFTER AddNetworkModule: hosted services start in the order they are registered, and the seed
    // needs the stations that Network loads first.
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityModuleDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("SmartRail")
                ?? throw new InvalidOperationException("No connection string. See the message of the Network module for how to set it.");

            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityModuleDbContext.Schema));
        });

        // ASP.NET Core Identity does the risky parts: password hashing and lockout (ADR-10).
        services.AddIdentityCore<StaffAccount>(options =>
            {
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;
            })
            .AddEntityFrameworkStores<IdentityModuleDbContext>();

        // Read lazily, so a test or the host can supply the key through configuration after this method has run.
        services.AddSingleton(provider => JwtSettings.FromConfiguration(provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton<AccessTokenIssuer>();

        // Every request carrying "Authorization: Bearer <access token>" is checked here.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtSettings, TimeProvider>((options, jwt, clock) =>
            {
                // A cab token is only as good as its session: checked on every request that carries crewSessionId.
                options.Events = new JwtBearerEvents { OnTokenValidated = CabSessionCheck.ValidateAsync };
                options.MapInboundClaims = false;   // keep the claim names as issued: "sub", "role", "stationId"
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(jwt.SigningKey),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],   // refuse a token that names another algorithm
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.Zero,   // the default allows 5 extra minutes; the contract says 15 minutes
                    // Expiry is judged on the application's clock (which tests can fix), not the machine's.
                    LifetimeValidator = (notBefore, expires, _, _) =>
                        expires is not null
                        && clock.GetUtcNow().UtcDateTime < expires
                        && (notBefore is null || clock.GetUtcNow().UtcDateTime >= notBefore),
                };
            });

        services.AddAuthorization(Policies.Register);
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemAuthorizationResultHandler>();

        services.AddScoped<IdentitySeeder>();
        services.AddScoped<IStaffDirectory, StaffDirectory>();   // the door LiveOperations uses to find a driver's account id
        services.AddHostedService<IdentityStartupService>();

        return services;
    }

    // The host decides the URL prefix (/api/v1) and passes the group in.
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder routes)
    {
        StaffSignInEndpoint.Map(routes);
        CrewSignInEndpoint.Map(routes);
        CabSignInEndpoint.Map(routes);
        RefreshSessionEndpoint.Map(routes);
        SignOutEndpoint.Map(routes);
        GetStaffMeEndpoint.Map(routes);
        return routes;
    }
}

// Runs once when the application starts: checks the signing key, brings the "identity" schema up to date
// (Development only, like the other modules), then loads the staff and cab-device seed.
// If anything fails, the application does not start.
internal sealed class IdentityStartupService(IServiceProvider services, IHostEnvironment environment, IConfiguration configuration)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _ = services.GetRequiredService<JwtSettings>();   // throws with the exact command when the signing key is missing
        DummyPasswordCheck.Prepare();

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();

        if (environment.IsDevelopment())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The identity database is missing migrations ({string.Join(", ", pending)}). Apply them before starting the application.");
            }
        }

        var seedFolder = IdentitySeedFolder.Find(configuration["Seed:Path"]);
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync(seedFolder, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
