namespace SmartRail.LiveOperations.Features.Decisions;

// The bodies of the controller's decisions (contract section 3.5). Every field is nullable so a missing one is a 400 with a message,
// not a binding failure. `ExpectedVersion` is the journey's control version the controller was looking at (R-18, C-2).
public sealed record DelayRequest(int? Minutes, string? ReasonCode, string? Note, int? ExpectedVersion);

// Used by the decisions that need nothing but the version (clear the delay, release a hold, reinstate).
public sealed record VersionRequest(int? ExpectedVersion);
