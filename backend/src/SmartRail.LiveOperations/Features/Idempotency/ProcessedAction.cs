namespace SmartRail.LiveOperations.Features.Idempotency;

// A crew action (start, end, a batch of positions) that has been done, with the answer that was given. The tablet generates the
// clientActionId and sends it again when it is not sure the first request arrived (a tunnel, a dropped connection); a repeat gets
// the original answer and changes nothing (contract section 1, C-7). Kept for 48 hours.
internal sealed class ProcessedAction
{
    public Guid ClientActionId { get; set; }
    public Guid JourneyId { get; set; }
    public string Kind { get; set; } = "";     // "Start", "End", "Positions"
    public string ResultJson { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
