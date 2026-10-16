namespace SmartRail.LiveOperations.Outbox;

internal enum OutboxStatus { Pending, Dispatched, Failed }

// One announced fact, written in the same transaction as the state change that caused it (architecture principles:
// "announcing a fact" goes through the outbox). The table is the module's event log: the dispatcher sends Pending rows
// to the stream, and dispatched rows stay for 24 h so a client can resume with Last-Event-ID.
internal sealed class OutboxEvent
{
    public long Id { get; set; }                  // insertion order, assigned by the database
    public Guid EventId { get; set; }             // the envelope's eventId; consumers ignore a repeat
    public string Type { get; set; } = "";
    public DateTime OccurredAt { get; set; }      // when it happened (corrected time, R-09)
    public DateTime CreatedAt { get; set; }       // when the row was written (TimeProvider)
    public Guid JourneyId { get; set; }
    public string Json { get; set; } = "";        // the full envelope as the ops side sees it, including source
    public OutboxStatus Status { get; set; }
    public int Attempts { get; set; }
    public long? Sequence { get; set; }           // the stream sequence; given by the dispatcher, in dispatch order
    public DateTime? DispatchedAt { get; set; }
}
