import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';
import 'package:smartrail_passenger/main.dart';

import '../support/fake_backend.dart';
import 'details/details_screen_test.dart' show backendWith, pumpDetails;
import 'tracking/tracking_screen_test.dart' show pumpTracking, trackingTest;

// Rehearsal #8: while the controller has a hold set (the API's `hold` field), the journey shows the Held status of the fixtures
// (S-21 "Held", S-08 "Held · N min late") with the hand-stop icon; the reason stays under it. When the hold is released the badge goes.
// The backend sends a hold as a flag and keeps the status kind (D-4), so the app maps the flag to the Held display, adds no minutes.

Map<String, dynamic> holdAt(String stationId) => {
      'stationId': stationId,
      'minutes': 10,
      'reason': {'code': 'Congestion', 'note': null},
    };

Map<String, dynamic> heldDetail({Map<String, dynamic>? status}) {
  final detail = detailJson()..['hold'] = holdAt(polgahawelaId);
  if (status != null) detail['status'] = status;
  return detail;
}

void main() {
  group('train details', () {
    testWidgets('hold set on a train that is on time: the Held badge (S-21) with the hand icon, and the reason under it', (tester) async {
      await pumpDetails(tester, backendWith(heldDetail()));

      expect(find.text('Held'), findsOneWidget);
      expect(find.text('On time'), findsNothing);
      expect(find.byIcon(Icons.pan_tool), findsWidgets);
      expect(find.text('Reason: Congestion'), findsOneWidget);
    });

    testWidgets('hold set on a late train: "Held · 8 min late" (S-08), the delay the API sent, nothing added', (tester) async {
      await pumpDetails(tester, backendWith(heldDetail(status: {'kind': 'Late', 'delayMinutes': 8})));

      expect(find.text('Held · 8 min late'), findsOneWidget);
      expect(find.text('8 min late'), findsNothing);
    });

    testWidgets('the API already says Held: shown as it is', (tester) async {
      await pumpDetails(tester, backendWith(heldDetail(status: {'kind': 'Held', 'delayMinutes': 3})));

      expect(find.text('Held · 3 min late'), findsOneWidget);
    });

    testWidgets('no hold: no Held badge (released)', (tester) async {
      await pumpDetails(tester, backendWith(detailJson()));

      expect(find.textContaining('Held'), findsNothing);
      expect(find.text('On time'), findsOneWidget);
    });

    testWidgets('a hold does not hide a cancelled journey', (tester) async {
      await pumpDetails(tester, backendWith(heldDetail(status: {'kind': 'Cancelled', 'delayMinutes': null})));

      expect(find.text('Cancelled'), findsOneWidget);
      expect(find.textContaining('Held'), findsNothing);
    });
  });

  group('waiting (tracking)', () {
    trackingTest('shows Held while the hold is set', (tester) async {
      await pumpTracking(tester, backendWith(heldDetail()));

      expect(find.text('Held'), findsWidgets);
      expect(find.text('On time'), findsNothing);
    });

    trackingTest('no Held once it is released', (tester) async {
      await pumpTracking(tester, backendWith(detailJson()));

      expect(find.textContaining('Held'), findsNothing);
    });
  });

  group('search results', () {
    testWidgets('a held train says Held on its card; a released one does not', (tester) async {
      tester.view.physicalSize = const Size(390 * 2, 844 * 2);
      tester.view.devicePixelRatio = 2;
      addTearDown(tester.view.reset);
      final held = journeyJson(id: 'a', serviceNumber: '1001')..['hold'] = holdAt(polgahawelaId);
      final free = journeyJson(id: 'b', serviceNumber: '1002', name: 'Kandy Slow');
      final backend = FakeBackend()..journeys = [held, free];
      await tester.pumpWidget(PassengerApp(api: SmartRailApi(backend.client, 'http://test'), nowUtc: () => DateTime.utc(2026, 10, 13, 20)));

      await tester.enterText(find.widgetWithText(TextField, Strings.from), 'Gam');
      await tester.pump(const Duration(milliseconds: 400));
      await tester.pump();
      await tester.tap(find.text('Gampaha').last);
      await tester.pump();
      await tester.enterText(find.widgetWithText(TextField, Strings.to), 'Kan');
      await tester.pump(const Duration(milliseconds: 400));
      await tester.pump();
      await tester.tap(find.text('Kandy').last);
      await tester.pump();
      await tester.tap(find.text(Strings.search));
      await tester.pumpAndSettle();

      expect(find.text('Held'), findsOneWidget);
      expect(find.text('On time'), findsOneWidget);
    });
  });
}
