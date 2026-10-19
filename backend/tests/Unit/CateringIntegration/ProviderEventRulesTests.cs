using System.Text.Json;
using SmartRail.CateringIntegration.Relay;
using SmartRail.LiveOperations;
using Xunit;

namespace SmartRail.UnitTests.CateringIntegration;

// Which webhooks a journey event causes (US-F2): only catering stops, only real changes, no passenger data.
public class ProviderEventRulesTests
{
    private static readonly Guid Polgahawela = Guid.NewGuid();
    private static readonly Guid Peradeniya = Guid.NewGuid();   // a station without catering
    private static readonly Guid Journey = Guid.NewGuid();
    private static readonly DateTime Scheduled = new(2026, 10, 20, 2, 43, 0, DateTimeKind.Utc);
    private static readonly IReadOnlyDictionary<Guid, string> Refs = new Dictionary<Guid, string> { [Polgahawela] = "PLG" };

    private static JourneyFeedEvent Source(string type) =>
        new(1, Guid.NewGuid(), type, Scheduled, Journey, new DateOnly(2026, 10, 20), JsonSerializer.SerializeToElement(new { }));

    private static JourneyCall Call(Guid station, string status, int minutesLate, bool stops = true) =>
        new(station, 1, stops, status, Scheduled, Scheduled.AddMinutes(minutesLate));

    private static JourneyState State(string status, params JourneyCall[] calls) =>
        new(Journey, Guid.NewGuid(), new DateOnly(2026, 10, 20), status, "TrainFault", "Engine failure", calls);

    private static IReadOnlyList<ProviderEvent> Make(string type, JourneyState state, Dictionary<string, DateTime>? lastSent = null) =>
        ProviderEventRules.Create(Source(type), state, "1001", Refs, lastSent ?? [], Guid.NewGuid);

    [Fact]
    public void A_delay_gives_one_ETA_for_the_catering_stop_the_train_has_not_reached()
    {
        var result = Make("DelayChanged", State("Scheduled", Call(Polgahawela, "Scheduled", 7), Call(Peradeniya, "Scheduled", 7)));

        var eta = Assert.Single(result);
        Assert.Equal("provider.eta-updated", eta.Type);
        Assert.Equal("PLG", eta.StationRef);
        Assert.Equal("1001", eta.ServiceNumber);
        Assert.Equal(Scheduled.AddMinutes(7), eta.ExpectedArrival);
        Assert.Equal(Scheduled, eta.ScheduledArrival);
    }

    [Fact]
    public void A_delay_says_nothing_about_a_catering_stop_the_train_has_already_left()
    {
        Assert.Empty(Make("DelayChanged", State("Running", Call(Polgahawela, "Departed", 7))));
    }

    [Fact]
    public void An_ETA_that_differs_less_than_a_minute_from_the_last_one_told_is_not_sent_again()
    {
        var state = State("Running", Call(Polgahawela, "Scheduled", 7));

        Assert.Empty(Make("DelayChanged", state, new() { ["PLG"] = Scheduled.AddMinutes(7).AddSeconds(30) }));
        Assert.Single(Make("DelayChanged", state, new() { ["PLG"] = Scheduled.AddMinutes(5) }));
    }

    [Fact]
    public void The_first_ETA_is_compared_with_the_schedule_so_an_on_time_train_sends_nothing()
    {
        Assert.Empty(Make("DelayChanged", State("Running", Call(Polgahawela, "Scheduled", 0))));
    }

    [Fact]
    public void A_cancelled_journey_tells_the_provider_with_the_reason_as_plain_text()
    {
        var result = Make("JourneyCancelled", State("Cancelled", Call(Polgahawela, "Skipped", 0), Call(Peradeniya, "Skipped", 0)));

        var changed = Assert.Single(result);
        Assert.Equal("provider.journey-changed", changed.Type);
        Assert.Equal("Cancelled", changed.Change);
        Assert.Equal("TrainFault: Engine failure", changed.Reason);
    }

    [Fact]
    public void An_early_termination_tells_only_about_the_catering_stops_that_will_not_be_reached()
    {
        var result = Make("JourneyTerminatedEarly", State("TerminatedEarly", Call(Polgahawela, "Skipped", 0)));
        Assert.Equal("Terminated", Assert.Single(result).Change);

        Assert.Empty(Make("JourneyTerminatedEarly", State("TerminatedEarly", Call(Polgahawela, "Departed", 0))));
    }

    [Fact]
    public void A_cancellation_that_has_been_reinstated_since_says_nothing()
    {
        Assert.Empty(Make("JourneyCancelled", State("Scheduled", Call(Polgahawela, "Scheduled", 0))));
    }

    [Fact]
    public void Other_event_types_are_not_relevant()
    {
        Assert.False(ProviderEventRules.IsRelevant("PlatformChanged"));
        Assert.True(ProviderEventRules.IsRelevant("DelayChanged"));
    }
}
