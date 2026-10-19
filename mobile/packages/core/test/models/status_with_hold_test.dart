import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

// A hold is a flag, not a status (D-4); the app maps the flag to the Held display (fixtures S-21, S-08). Nothing is added to the delay.
void main() {
  const hold = JourneyHold(stationId: 's', minutes: 10, reason: null);

  test('no hold: the status as sent', () {
    const status = JourneyStatus(StatusKind.late, delayMinutes: 8);
    expect(status.shownWithHold(null), same(status));
  });

  test('a hold on a running journey: Held with the delay the API sent', () {
    final shown = const JourneyStatus(StatusKind.late, delayMinutes: 8).shownWithHold(hold);
    expect(shown.kind, StatusKind.held);
    expect(shown.delayMinutes, 8);
    expect(statusText(shown), 'Held · 8 min late'); // S-08
  });

  test('a hold on an on-time journey: "Held" (S-21)', () {
    final shown = const JourneyStatus(StatusKind.onTime, delayMinutes: 0).shownWithHold(hold);
    expect(statusText(shown), 'Held');
  });

  test('cancelled, ended early and completed keep their own status', () {
    for (final kind in [StatusKind.cancelled, StatusKind.terminatedEarly, StatusKind.completed]) {
      final status = JourneyStatus(kind);
      expect(status.shownWithHold(hold), same(status));
    }
  });
}
