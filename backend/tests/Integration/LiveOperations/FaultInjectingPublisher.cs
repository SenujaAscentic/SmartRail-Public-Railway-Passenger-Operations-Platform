using System.Collections.Concurrent;
using SmartRail.LiveOperations.Stream;

namespace SmartRail.IntegrationTests.LiveOperations;

// Test infrastructure, not part of the application: stands in front of the real stream hub and refuses the events of
// chosen journeys, so a test can see what the dispatcher does when handing over fails (retry, then give up).
internal sealed class FaultInjectingPublisher(IEventPublisher inner) : IEventPublisher
{
    public static ConcurrentDictionary<Guid, byte> FailingJourneys { get; } = new();

    public void Publish(PublicEvent publicEvent)
    {
        if (FailingJourneys.ContainsKey(publicEvent.JourneyId))
        {
            throw new InvalidOperationException("Injected failure for a test.");
        }

        inner.Publish(publicEvent);
    }
}
