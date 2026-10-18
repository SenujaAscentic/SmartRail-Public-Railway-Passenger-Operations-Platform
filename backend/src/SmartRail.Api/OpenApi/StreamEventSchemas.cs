using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using SmartRail.LiveOperations.Features.StreamEvents;
using SmartRail.LiveOperations.Stream;

namespace SmartRail.Api.OpenApi;

// Puts the stream's events into openapi.yaml, so the web and Flutter apps generate their event types from the description
// instead of writing them by hand (CLAUDE.md section 5, step 4). An SSE endpoint has no response type the framework could read,
// so the shapes are registered here from the records in LiveOperations (Stream/StreamEventSchemas.cs, SnapshotReader.cs).
internal static class StreamEventSchemas
{
    // Each event type and the value its `type` field always has.
    private static readonly (Type Clr, string TypeName)[] Events =
    [
        (typeof(StopStatusChangedEvent), "StopStatusChanged"),
        (typeof(DelayChangedEvent), "DelayChanged"),
        (typeof(DataFreshnessChangedEvent), "DataFreshnessChanged"),
        (typeof(JourneyStartedEvent), "JourneyStarted"),
        (typeof(JourneyEndedEvent), "JourneyEnded"),
    ];

    public static OpenApiOptions AddStreamEventSchemas(this OpenApiOptions options)
    {
        // `type` is a string in the records; in the description it is the one value of that event, which is what lets a client
        // (and the generated TypeScript) tell the events of the union apart.
        options.AddSchemaTransformer((schema, context, cancellationToken) =>
        {
            // Enums are sent by name, so say so: a list of values with no type is not enough for every generator.
            if (schema.Enum is { Count: > 0 } && schema.Type is null)
            {
                schema.Type = JsonSchemaType.String;
            }

            if (context.JsonPropertyInfo is { Name: "type" } property
                && Events.FirstOrDefault(e => e.Clr == property.DeclaringType) is { Clr: not null } found)
            {
                schema.Enum = [JsonValue.Create(found.TypeName)];
            }

            return Task.CompletedTask;
        });

        options.AddDocumentTransformer(async (document, context, cancellationToken) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

            // The framework builds the schema of a type (its nested types become references) but does not put it under
            // components.schemas by itself, so each one is added here under its type name.
            foreach (var clr in Events.Select(e => e.Clr).Append(typeof(JourneySnapshot)).Append(typeof(StreamResync)))
            {
                document.Components.Schemas[clr.Name] = await context.GetOrCreateSchemaAsync(clr, null, cancellationToken);
            }

            document.Components.Schemas["StreamEvent"] = new OpenApiSchema
            {
                Description = "The data of a live event frame (`event:` is the type, `id:` the sequence). Told apart by `type`.",
                OneOf = [.. Events.Select(e => (IOpenApiSchema)new OpenApiSchemaReference(e.Clr.Name, document))],
                Discriminator = new OpenApiDiscriminator
                {
                    PropertyName = "type",
                    Mapping = Events.ToDictionary(e => e.TypeName, e => new OpenApiSchemaReference(e.Clr.Name, document)),
                },
            };
        });

        options.AddOperationTransformer((operation, context, cancellationToken) =>
        {
            if (operation.OperationId == "StreamEvents" && operation.Responses?.TryGetValue("200", out var ok) == true && ok is OpenApiResponse response)
            {
                response.Description =
                    "A Server-Sent Events stream. Frames: `snapshot` (data: JourneySnapshot), `resync` (data: StreamResync, no id), "
                    + "and the live events StopStatusChanged, DelayChanged and DataFreshnessChanged (data: StreamEvent; id = the stream sequence). "
                    + "Comment lines (`: keep-alive`) are heartbeats.";
                response.Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["text/event-stream"] = new() { Schema = new OpenApiSchemaReference("StreamEvent", context.Document) },
                };
            }

            return Task.CompletedTask;
        });

        return options;
    }
}
