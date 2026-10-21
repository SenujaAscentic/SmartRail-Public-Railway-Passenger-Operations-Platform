import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

import '../support/fake_backend.dart';
import 'details/details_screen_test.dart' show backendWith, pumpDetails;
import 'details/disruptions_test.dart' show cancelled, skip, terminatedAtGampaha;
import 'tracking/tracking_screen_test.dart' show pumpTracking, trackingTest;

// Found by E2E flow 3 on the station display, checked here for the passenger app: a cancelled journey has EVERY stop Skipped and a
// journey that ends early has every stop after the end Skipped (contract 3.3). The journey's own status wins: the screens say
// "Cancelled" (S-06) / "Terminates at X" (S-07), never "Does not stop", and never show those stops as if the train would run.

List<StopRow> stopRows(WidgetTester tester) => tester.widgetList<StopRow>(find.byType(StopRow)).toList();

void main() {
  setUpAll(loadAppFonts);

  group('train details: a cancelled journey', () {
    testWidgets('says Cancelled once and never "Does not stop"', (tester) async {
      await pumpDetails(tester, backendWith(cancelled()), boarding: gampahaId, destination: kandyId);

      expect(find.text('Cancelled'), findsOneWidget);
      expect(find.text('Does not stop'), findsNothing);
    });

    testWidgets('lists the stops, none of them marked as skipped', (tester) async {
      await pumpDetails(tester, backendWith(cancelled()), boarding: gampahaId, destination: kandyId);

      final rows = stopRows(tester);
      expect(rows, isNotEmpty);
      expect(rows.where((r) => r.state == StopState.skipped), isEmpty);
      expect(find.text('Polgahawela'), findsOneWidget);
    });

    testWidgets('a stop skipped on a journey that runs is still "Does not stop" (S-12)', (tester) async {
      final detail = detailJson();
      skip(detail, ['Polgahawela']);
      await pumpDetails(tester, backendWith(detail), boarding: gampahaId, destination: kandyId);

      expect(find.text('Does not stop'), findsOneWidget);
    });
  });

  group('train details: a journey that ends early', () {
    testWidgets('says "Terminates at Gampaha" once and never "Does not stop" for the stops after it', (tester) async {
      await pumpDetails(tester, backendWith(terminatedAtGampaha()), boarding: gampahaId, destination: kandyId);

      expect(find.text('Terminates at Gampaha'), findsOneWidget);
      expect(find.text('Does not stop'), findsNothing);
      expect(stopRows(tester).where((r) => r.state == StopState.skipped), isEmpty);
    });
  });

  group('waiting (tracking)', () {
    trackingTest('a journey that ends before the passenger\'s station: "Terminates at Gampaha", no "Expected at" time', (tester) async {
      await pumpTracking(tester, backendWith(terminatedAtGampaha()), boarding: peradeniyaId, destination: kandyId);

      expect(find.text('Terminates at Gampaha'), findsWidgets);
      expect(find.text('Terminates early'), findsNothing, reason: 'S-07 with the station, not S-23');
      expect(find.textContaining('Expected at'), findsNothing);
      expect(find.text('Does not stop'), findsNothing);
    });

    trackingTest('a cancelled journey: "Cancelled", no "Expected at" time', (tester) async {
      await pumpTracking(tester, backendWith(cancelled()), boarding: gampahaId, destination: kandyId);

      expect(find.text('Cancelled'), findsWidgets);
      expect(find.textContaining('Expected at'), findsNothing);
      expect(find.text('Does not stop'), findsNothing);
    });
  });

  group('on board (tracking)', () {
    trackingTest('a cancelled journey: the card says Cancelled, not an arrival time', (tester) async {
      final controller = await pumpTracking(tester, backendWith(cancelled()), boarding: gampahaId, destination: kandyId);
      expect(controller, isNotNull);
      await tester.tap(find.text(Strings.onBoard));
      await tester.pumpAndSettle();

      expect(find.text('Cancelled'), findsWidgets);
      expect(find.textContaining('Arrives at'), findsNothing);
    });
  });
}
