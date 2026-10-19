import 'package:smartrail_core/smartrail_core.dart';

// What the tracking screen picks out of a journey. It only reads what the
// backend sent (the state of each stop); it works out no time and no status.

/// The last stop the train has gone through (stopped at and left, or passed),
/// including stations an express runs through; null before it has started.
JourneyStop? lastPassedStop(JourneyDetail journey) {
  JourneyStop? last;
  for (final s in journey.stops) {
    if (s.state == StopState.departed || s.state == StopState.passed) last = s;
  }
  return last;
}

/// When the train left or passed [stop]: the actual time, else what is expected.
DateTime? passedAt(JourneyStop stop) =>
    stop.actualDeparture ?? stop.actualArrival ?? stop.expectedDeparture ?? stop.expectedArrival;

/// The next calling point the train will stop at (or is stopped at): the first
/// one that is still Scheduled or Arrived. Null when there is none left.
JourneyStop? nextCallingStop(JourneyDetail journey) {
  for (final s in journey.callingPoints) {
    if (s.state == StopState.scheduled || s.state == StopState.arrived) return s;
  }
  return null;
}

/// Scheduled and expected arrival at [stop], as sent. At the origin there is no
/// arrival, so the departure is used. Once the train has arrived, the actual
/// arrival is the expected one.
({DateTime? scheduled, DateTime? expected}) arrivalTimes(JourneyStop stop) {
  final hasArrival = stop.scheduledArrival != null;
  return (
    scheduled: hasArrival ? stop.scheduledArrival : stop.scheduledDeparture,
    expected: hasArrival
        ? (stop.actualArrival ?? stop.expectedArrival)
        : (stop.actualDeparture ?? stop.expectedDeparture),
  );
}

/// The train has left the passenger's boarding station: time to ask "Are you on board?".
bool hasLeft(JourneyStop? boarding) => boarding != null && boarding.state == StopState.departed;
