using SmartRail.Network;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNetworkModule(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();

// Every endpoint lives under /api/v1 (contract section 1).
var api = app.MapGroup("/api/v1");
api.MapNetworkEndpoints();

app.Run();

// Lets the integration tests start the whole application in memory.
public partial class Program;
