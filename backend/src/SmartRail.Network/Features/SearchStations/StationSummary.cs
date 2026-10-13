namespace SmartRail.Network.Features.SearchStations;

// One row of the type-ahead list. The station detail endpoint (platforms, catering flag) is a separate feature.
public sealed record StationSummary(Guid Id, string Code, string Name);
