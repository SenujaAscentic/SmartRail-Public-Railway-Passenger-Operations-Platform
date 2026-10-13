using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using SmartRail.Network;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNetworkModule(builder.Configuration);
builder.Services.AddOpenApi(options =>
{
    // The server URL would be copied from whichever request produced the file; clients choose their own.
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Servers = null;
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
app.MapOpenApi("/openapi/{documentName}.yaml");

// Every endpoint lives under /api/v1 (contract section 1).
var api = app.MapGroup("/api/v1");
api.MapNetworkEndpoints();

app.Run();

// Lets the integration tests start the whole application in memory.
public partial class Program;
