import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

import '../support/fake_backend.dart';
import 'details/details_screen_test.dart' show backendWith, pumpDetails;
import 'tracking/tracking_screen_test.dart' show pumpTracking, trackingTest;

// Rehearsal #3: a train that has not started has no live "expected" time, only its schedule, and the screens say "Scheduled".

Map<String, dynamic> notStartedDetail() => detailJson()..['status'] = {'kind': 'NotStarted', 'delayMinutes': null};

void main() {
  group('train details', () {
    testWidgets('a train that has not started has one time column, "Sched.", and no "Exp."', (tester) async {
      await pumpDetails(tester, backendWith(notStartedDetail()), boarding: gampahaId, destination: kandyId);

      expect(find.text(Strings.scheduledColumn), findsOneWidget);
      expect(find.text(Strings.expectedColumn), findsNothing);
    });

    testWidgets('a train that runs keeps both columns', (tester) async {
      await pumpDetails(tester, backendWith(detailJson()), boarding: gampahaId, destination: kandyId);

      expect(find.text(Strings.scheduledColumn), findsOneWidget);
      expect(find.text(Strings.expectedColumn), findsOneWidget);
    });
  });

  group('waiting (tracking)', () {
    trackingTest('says "Scheduled at Gampaha", not "Expected at", for a train that has not started', (tester) async {
      await pumpTracking(tester, backendWith(notStartedDetail()));

      expect(find.text('Scheduled at Gampaha'), findsOneWidget);
      expect(find.text('Expected at Gampaha'), findsNothing);
      expect(find.text('Not started'), findsOneWidget);
    });

    trackingTest('still says "Expected at Gampaha" for a train that runs', (tester) async {
      await pumpTracking(tester, backendWith(detailJson()));

      expect(find.text('Expected at Gampaha'), findsOneWidget);
      expect(find.text('Scheduled at Gampaha'), findsNothing);
    });
  });
}
