using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using SmartRail.Identity;
using SmartRail.LiveOperations;
using SmartRail.Network;

var builder = WebApplication.CreateBuilder(args);

// One clock for the whole application; tests replace it with a fixed one.
builder.Services.AddSingleton(TimeProvider.System);

// Order matters: LiveOperations' start-up check needs the network data that Network loads first.
builder.Services.AddNetworkModule(builder.Configuration);
builder.Services.AddLiveOperationsModule(builder.Configuration);
builder.Services.AddIdentityModule(builder.Configuration);   // after Network: its seed needs the station IDs
builder.Services.AddOpenApi(options =>
{
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
