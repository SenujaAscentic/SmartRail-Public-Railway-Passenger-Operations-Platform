namespace SmartRail.Simulator;

// A held train does not leave (R-22): while the controller has a hold on a station, the driver waits there. The timeline is the
// timetable, so the simulator keeps its own "train time" that stops while the train is held and is the real time minus the total
// time spent held. Pure, so it is unit-tested; the simulator only feeds it what the public journey says.
public sealed class HoldGate
{
    private static readonly TimeSpan JustBeforeDeparture = TimeSpan.FromSeconds(1);

    // How much of the real time the train has spent held so far.
    public TimeSpan Held { get; private set; }

    // The time on the train's own clock.
    public DateTimeOffset TrainTime(DateTimeOffset now) => now - Held;

    // Called once per tick. `heldStop` is the index of the stop the journey says is held, or null. Returns true while the train is
    // being kept at that stop. A hold on a stop the train has not reached yet takes effect when it gets there; a hold on a stop the train
    // has already left (more than a tick ago) is ignored.
    public bool Update(DateTimeOffset now, Timeline timeline, int? heldStop, TimeSpan tick)
    {
        if (heldStop is not { } stop)
        {
            return false;
        }

        var trainTime = TrainTime(now);
        var leaves = timeline.DepartureAt(stop);
        if (trainTime < leaves - JustBeforeDeparture || trainTime >= leaves + tick)
        {
            return false;
        }

        // Stand still just before the departure time until the hold is lifted.
        Held = now - (leaves - JustBeforeDeparture);
        return true;
    }
}
