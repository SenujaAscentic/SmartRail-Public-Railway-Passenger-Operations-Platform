using SmartRail.CateringIntegration;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using SmartRail.Api.Clock;
using SmartRail.Api.OpenApi;
using SmartRail.Identity;
using SmartRail.LiveOperations;
using SmartRail.Network;

var builder = WebApplication.CreateBuilder(args);

// One clock for the whole application; tests replace it with a fixed one. It is the real clock, except in Development, where
// Clock:Start / Clock:Speed can move it for a demo (ClockSetup). Every module and Identity use THIS clock, so token issue and token
// validation always agree, whatever time the demo clock shows.
builder.Services.AddSingleton(provider => ClockSetup.Create(provider.GetRequiredService<IHostEnvironment>(), provider.GetRequiredService<IConfiguration>()));
builder.Services.AddHostedService<ClockGuard>();   // first to start: refuses the Clock settings outside Development

// JSON numbers are numbers: the ASP.NET default also accepts "12" as a number, and then the API description says
// "number or string" for every numeric field, which would give every client a union type to handle.
// Enums are sent by name, and the API description then lists the names ("Scheduled", "Arrived", ...) instead of numbers.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Order matters (hosted services start in this order): Network loads the network first; Identity seeds the staff accounts next;
// LiveOperations then checks the assignments and generates journeys, and needs both.
builder.Services.AddNetworkModule(builder.Configuration);
builder.Services.AddIdentityModule(builder.Configuration);   // after Network (its seed needs the station IDs) and before LiveOperations
builder.Services.AddLiveOperationsModule(builder.Configuration);   // its first journey generation needs the driver accounts Identity seeds
builder.Services.AddCateringIntegrationModule(builder.Configuration);   // reads LiveOperations' feed and Network's catalog, so it comes after them
builder.Services.AddOpenApi(options =>
{
    options.AddStreamEventSchemas();   // the live events and the snapshot, for the apps' generated types

    // The server URL would be copied from whichever request produced the file; clients choose their own.
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Servers = null;
        return Task.CompletedTask;
    });

    // Contract section 2: protected operations take "Authorization: Bearer <access token>". Declared once here;
    // an operation is marked as needing it when its endpoint names a policy (and does not allow anonymous callers).
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security =
            [
                new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [] },
            ];
        }

        return Task.CompletedTask;
    });

    // Contract section 6: every error is Problem Details plus a stable code, field errors and a request id.
    // The framework only knows the standard fields, so the extra ones are added to the description here,
    // once, for every module. (The responses themselves are built where the error happens.)
    options.AddSchemaTransformer((schema, context, cancellationToken) =>
    {
        if (context.JsonTypeInfo.Type == typeof(ProblemDetails))
        {
            schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
            schema.Properties["code"] = new OpenApiSchema { Type = JsonSchemaType.String };
            schema.Properties["requestId"] = new OpenApiSchema { Type = JsonSchemaType.String };
            // Only on 409 VERSION_CONFLICT (R-18): the journey's current version and the journey itself (a JourneyDetailResponse).
            schema.Properties["currentVersion"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" };
            schema.Properties["journey"] = new OpenApiSchema { Type = JsonSchemaType.Object };
            schema.Properties["errors"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                AdditionalProperties = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String },
                },
            };
            schema.Required ??= new HashSet<string>();
            schema.Required.Add("code");
            schema.Required.Add("requestId");
        }

        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Every response carries a Date header from OUR clock (TimeProvider). The passenger app shows "updated X ago" as
// (Date - the time stored in the response), which only works if both come from the same clock; the host would use its own.
// Kestrel adds a Date header only when the application has not set one, and the in-memory test server adds none at all.
var clock = app.Services.GetRequiredService<TimeProvider>();
app.Use((context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.Date = clock.GetUtcNow().ToString("r", CultureInfo.InvariantCulture);
        return Task.CompletedTask;
    });
    return next(context);
});

// The API description, as YAML (ADR-09). backend/openapi.yaml is a committed copy of this; a test compares them.
app.MapOpenApi("/openapi/{documentName}.yaml").AllowAnonymous();

// Who is calling (the access token), then whether that caller may do this (the endpoint's policy). Order matters.
app.UseAuthentication();
app.UseAuthorization();

// Every endpoint lives under /api/v1 (contract section 1).
var api = app.MapGroup("/api/v1");
api.MapNetworkEndpoints();
api.MapLiveOperationsEndpoints();
api.MapIdentityEndpoints();

app.Run();

// Lets the integration tests start the whole application in memory.
public partial class Program;
