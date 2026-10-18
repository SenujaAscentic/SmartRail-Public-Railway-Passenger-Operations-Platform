import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

// Cases the fixtures do not cover.
void main() {
  group('JourneyStatus.fromJson', () {
    test('Late without delayMinutes is refused, never shown as something else', () {
      expect(() => JourneyStatus.fromJson({'kind': 'Late'}), throwsFormatException);
    });

    test('an unknown kind is refused', () {
      expect(() => JourneyStatus.fromJson({'kind': 'Delayed'}), throwsFormatException);
    });
  });

  group('freshnessText', () {
    test('Live without an age shows nothing (no Date header, no age)', () {
      expect(freshnessText(const Freshness(Tracking.live)), isNull);
    });

    test('Stale without a station still says when it was last seen', () {
      final f = Freshness(Tracking.stale, lastSeenAt: DateTime.utc(2026, 10, 14, 2, 8));
      expect(freshnessText(f), 'Last seen 07:38');
    });
  });

  group('ageLabel', () {
    test('59 s stays in seconds', () => expect(ageLabel(const Duration(seconds: 59)), '59 s'));
    test('60 s is 1 min', () => expect(ageLabel(const Duration(seconds: 60)), '1 min'));
    test('119 s rounds down to 1 min', () => expect(ageLabel(const Duration(seconds: 119)), '1 min'));
  });
}
