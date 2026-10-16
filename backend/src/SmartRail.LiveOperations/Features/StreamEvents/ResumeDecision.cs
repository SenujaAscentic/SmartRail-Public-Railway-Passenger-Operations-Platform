namespace SmartRail.LiveOperations.Features.StreamEvents;

internal enum ResumeAction
{
    Fresh,    // no Last-Event-ID: send a snapshot, then live events
    Resume,   // send the events the client missed, then live events
    Resync,   // the client cannot be caught up from the log: tell it, send a snapshot, then live events
}

// Contract section 4 (Resume). Sequences have gaps (a failed dispatch uses one up), so the log cannot prove that nothing
// was deleted; the rule is the safe one: only a Last-Event-ID inside what is kept (or the one just before it) resumes.
// A false "resync" costs the client one snapshot reload; a false "resume" would silently lose events.
internal static class ResumeDecision
{
    public static ResumeAction Of(long? lastEventId, long? oldestRetained, long? newest)
    {
        if (lastEventId is not { } last)
        {
            return ResumeAction.Fresh;
        }

        if (oldestRetained is not { } oldest || newest is not { } latest)
        {
            return ResumeAction.Resync;   // nothing is kept, so nothing can be replayed
        }

        return last >= oldest - 1 && last <= latest ? ResumeAction.Resume : ResumeAction.Resync;
    }
}
