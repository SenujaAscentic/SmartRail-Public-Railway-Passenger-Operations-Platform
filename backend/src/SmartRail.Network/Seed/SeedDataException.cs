namespace SmartRail.Network.Seed;

// Thrown at startup when the seed files break an invariant. The application does not start.
public sealed class SeedDataException(IReadOnlyList<SeedIssue> errors)
    : Exception("The network seed data is invalid:" + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Select(e => $"  [{e.Rule}] {e.Message}")))
{
    public IReadOnlyList<SeedIssue> Errors { get; } = errors;
}
