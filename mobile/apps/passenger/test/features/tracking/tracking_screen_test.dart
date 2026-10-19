import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';
import 'package:smartrail_passenger/features/details/details_controller.dart';
import 'package:smartrail_passenger/features/tracking/tracking_facts.dart';
import 'package:smartrail_passenger/features/tracking/tracking_screen.dart';

import '../../support/fake_backend.dart';
import '../details/details_screen_test.dart' show FakeStopwatch, thirtySecondsLater;

// US-P3: waiting and on-board mode, and "Are you on board?" (E-24, E-25).
// The fixture: train 1001 between Kelaniya and Ragama, on time; the passenger
// boards at Gampaha (arrives 07:10, leaves 07:12 Colombo time) for Kandy (09:15).

const journeyId = '01a156ce-00e7-729d-a368-ef00172f4ec5';

final controllers = <DetailController>[];

/// A test that stops the controller's stream and timers when it ends (the test
/// framework checks for pending timers before tearDown runs).
void trackingTest(String name, Future<void> Function(WidgetTester tester) body) {
  testWidgets(name, (tester) async {
    await body(tester);
    for (final c in controllers) {
      c.stopLive();
    }
    controllers.clear();
    await tester.pumpWidget(const SizedBox());
    await tester.pump();
  });
}

Future<DetailController> pumpTracking(
  WidgetTester tester,
  FakeBackend backend, {
  String? boarding = gampahaId,
  String? destination = kandyId,
  List<JourneySummary>? opened,
}) async {
  tester.view.physicalSize = const Size(390 * 2, 1700 * 2);
  tester.view.devicePixelRatio = 2;
  addTearDown(tester.view.reset);
  final controller = DetailController(
    SmartRailApi(backend.client, 'http://test'),
    journeyId,
    boardingStationId: boarding,
    destinationStationId: destination,
    newStopwatch: FakeStopwatch.new,
  );
  controllers.add(controller);
  unawaited(controller.load());
  await tester.pump();
  await tester.pump();
  await tester.pumpWidget(
    MaterialApp(
      theme: buildPassengerTheme(Brightness.light),
      home: TrackingScreen(controller: controller, onOpenJourney: (j) => opened?.add(j)),
    ),
  );
  await tester.pump();
  await tester.pump();
  // Lets the sheet "Are you on board?" finish sliding in, when it is asked.
  await tester.pump(const Duration(milliseconds: 500));
  return controller;
}

FakeBackend backendWith(Map<String, dynamic> detail) => FakeBackend()
  ..detail = detail
  ..dateHeader = thirtySecondsLater;

/// Gampaha has been left at 07:12:30 Colombo time (01:42:30 UTC).
Map<String, dynamic> gampahaDeparted() {
  final detail = detailJson();
  final g = stopNamed(detail, 'Gampaha');
  g['status'] = 'Departed';
  g['actualArrival'] = '2026-10-20T01:40:10Z';
  g['actualDeparture'] = '2026-10-20T01:42:30Z';
  return detail;
}

Future<void> settle(WidgetTester tester, [Duration d = Duration.zero]) async {
  await tester.pump();
  await tester.pump();
  await tester.pump(d);
  await tester.pump();
  await tester.pump();
}

JourneyDetail parse(Map<String, dynamic> json) => JourneyDetail.fromJson(json);

void main() {
  setUpAll(loadAppFonts);

  group('what is picked out of the journey (pure)', () {
    test('lastPassedStop is the last stop the train has gone through, even one it only passes', () {
      expect(lastPassedStop(parse(detailJson()))!.name, 'Kelaniya');
    });

    test('lastPassedStop is null before the train has started', () {
      final detail = detailJson();
      for (final s in detail['stops'] as List) {
        (s as Map<String, dynamic>)['status'] = 'Scheduled';
      }
      expect(lastPassedStop(parse(detail)), isNull);
    });

    test('passedAt prefers the actual time', () {
      final kelaniya = parse(detailJson()).stopAt('01a14eb2-015b-7ebe-998e-19f3d315ef4d')!;
      expect(passedAt(kelaniya), DateTime.utc(2026, 10, 20, 1, 13, 30, 829, 592));
    });

    test('nextCallingStop is the first calling point still Scheduled', () {
      expect(nextCallingStop(parse(detailJson()))!.name, 'Gampaha');
      expect(nextCallingStop(parse(gampahaDeparted()))!.name, 'Polgahawela');
    });

    test('nextCallingStop includes the stop the train is at', () {
      final detail = detailJson();
      stopNamed(detail, 'Gampaha')['status'] = 'Arrived';
      expect(nextCallingStop(parse(detail))!.name, 'Gampaha');
    });

    test('nextCallingStop skips skipped stops and a stop it only passes', () {
      final detail = gampahaDeparted();
      stopNamed(detail, 'Polgahawela')['status'] = 'Skipped';
      expect(nextCallingStop(parse(detail))!.name, 'Peradeniya');
    });

    test('nextCallingStop is null when nothing is left', () {
      final detail = detailJson();
      for (final s in detail['stops'] as List) {
        (s as Map<String, dynamic>)['status'] = 'Departed';
      }
      expect(nextCallingStop(parse(detail)), isNull);
    });

    test('arrivalTimes: the arrival, or the departure at the origin', () {
      final j = parse(detailJson());
      final gampaha = arrivalTimes(j.stopAt(gampahaId)!);
      expect(gampaha.scheduled, DateTime.utc(2026, 10, 20, 1, 40));
      final origin = arrivalTimes(j.stops.first);
      expect(origin.scheduled, DateTime.utc(2026, 10, 20, 1, 0));
    });

    test('arrivalTimes: after the arrival, the actual time is the expected one', () {
      final detail = detailJson();
      final g = stopNamed(detail, 'Gampaha');
      g['status'] = 'Arrived';
      g['actualArrival'] = '2026-10-20T01:47:00Z';
      expect(arrivalTimes(parse(detail).stopAt(gampahaId)!).expected, DateTime.utc(2026, 10, 20, 1, 47));
    });

    test('hasLeft is true only for a Departed stop', () {
      final j = parse(gampahaDeparted());
      expect(hasLeft(j.stopAt(gampahaId)), isTrue);
      expect(hasLeft(parse(detailJson()).stopAt(gampahaId)), isFalse);
      expect(hasLeft(null), isFalse);
    });
  });

  group('waiting', () {
    trackingTest('is the mode at the start: the card says when the train is expected at the boarding station', (tester) async {
      await pumpTracking(tester, backendWith(detailJson()));

      expect(tester.widget<SegmentedButton<TrackingMode>>(find.byType(SegmentedButton<TrackingMode>)).selected, {TrackingMode.waiting});
      expect(find.text('Expected at Gampaha'), findsOneWidget);
      expect(find.text('07:10'), findsOneWidget, reason: 'arrival at Gampaha, 01:40 UTC in Colombo time');
      expect(find.text('On time'), findsOneWidget);
      expect(find.text('Scheduled'), findsNothing, reason: 'nothing to compare when they are the same');
    });

    trackingTest('shows the platform of the boarding station', (tester) async {
      await pumpTracking(tester, backendWith(detailJson()));

      expect(find.text('Platform'), findsOneWidget);
      expect(find.text('1'), findsOneWidget);
    });

    trackingTest('a delay: the expected time big, the scheduled time struck through, the minutes', (tester) async {
      final detail = detailJson()..['status'] = {'kind': 'Late', 'delayMinutes': 7};
      stopNamed(detail, 'Gampaha')['expectedArrival'] = '2026-10-20T01:47:00Z';
      await pumpTracking(tester, backendWith(detail));

      expect(find.text('07:17'), findsOneWidget);
      expect(find.textContaining('Scheduled', findRichText: true), findsOneWidget);
      expect(find.textContaining('07:10', findRichText: true), findsOneWidget);
      expect(find.text('7 min late'), findsOneWidget);
    });

    trackingTest('a platform change shows "Platform 5 (was 2)"', (tester) async {
      final detail = detailJson();
      final g = stopNamed(detail, 'Gampaha');
      g['platform'] = '5';
      g['previousPlatform'] = '2';
      await pumpTracking(tester, backendWith(detail));

      expect(find.text('Platform 5 (was 2)'), findsOneWidget);
    });

    trackingTest('where the train is: diagram, position, last passed station and the age', (tester) async {
      await pumpTracking(tester, backendWith(detailJson()));

      expect(find.text('Where the train is'), findsOneWidget);
      final diagram = tester.widget<LineDiagram>(find.byType(LineDiagram));
      expect(diagram.from, 'Kelaniya');
      expect(diagram.to, 'Ragama');
      expect(find.text('Between Kelaniya and Ragama'), findsOneWidget);
      // Kelaniya: passed at 01:13:30 UTC = 06:43 in Colombo.
      expect(find.text('Last passed Kelaniya at 06:43'), findsOneWidget);
      expect(find.text('Updated 30 s ago'), findsOneWidget);
    });

    trackingTest('the train is at the boarding station: "Arrived at Gampaha" with the actual time', (tester) async {
      final detail = detailJson();
      final g = stopNamed(detail, 'Gampaha');
      g['status'] = 'Arrived';
      g['actualArrival'] = '2026-10-20T01:41:00Z';
      g['expectedArrival'] = '2026-10-20T01:41:00Z';
      await pumpTracking(tester, backendWith(detail));

      expect(find.text('Arrived at Gampaha'), findsOneWidget);
      expect(find.text('07:11'), findsOneWidget);
      expect(find.text('At Gampaha'), findsOneWidget);
    });

    trackingTest('stale data: the banner, and the position stays', (tester) async {
      final detail = detailJson();
      (detail['freshness'] as Map)['tracking'] = 'Stale';
      await pumpTracking(tester, backendWith(detail));

      expect(find.text('Last seen 06:46 near Kelaniya'), findsOneWidget);
      expect(find.byType(LineDiagram), findsOneWidget);
    });

    trackingTest('lost data: the banner and no position', (tester) async {
      final detail = detailJson();
      (detail['freshness'] as Map)['tracking'] = 'Lost';
      await pumpTracking(tester, backendWith(detail));

      expect(find.text('Live location unavailable – scheduled times shown'), findsOneWidget);
      expect(find.byType(LineDiagram), findsNothing);
    });

    trackingTest('the reason of a delay is shown', (tester) async {
      final detail = detailJson()..['reason'] = {'code': 'SignalFailure', 'note': 'signal fault near Ragama'};
      await pumpTracking(tester, backendWith(detail));

      expect(find.text('Reason: Signal failure · signal fault near Ragama'), findsOneWidget);
    });
  });

  group('on board', () {
    trackingTest('"I\'m on this train" switches to on board: next stop and arrival at the destination', (tester) async {
      await pumpTracking(tester, backendWith(detailJson()));

      await tester.tap(find.text("I'm on this train"));
      await tester.pump();

      expect(tester.widget<SegmentedButton<TrackingMode>>(find.byType(SegmentedButton<TrackingMode>)).selected, {TrackingMode.onBoard});
      expect(find.text('Expected at Gampaha'), findsNothing);
      expect(find.text('Next stop'), findsOneWidget);
      expect(find.text('Gampaha'), findsOneWidget);
      expect(find.text('Platform 1'), findsOneWidget);
      expect(find.text('Arrives at Kandy'), findsOneWidget);
      expect(find.text('09:15'), findsOneWidget);
      expect(find.text('Stop tracking'), findsOneWidget);
      expect(find.text("I'm on this train"), findsNothing);
    });

    trackingTest('the next stop moves on when the train has left a stop', (tester) async {
      await pumpTracking(tester, backendWith(gampahaDeparted()));
      // (the prompt for the boarding station is open: answer yes)
      await tester.tap(find.text("Yes, I'm on board"));
      await tester.pumpAndSettle();

      expect(find.text('Polgahawela'), findsOneWidget);
      expect(find.text('08:13'), findsOneWidget);
    });

    trackingTest('a late train: expected arrival big, scheduled struck through', (tester) async {
      final detail = detailJson()..['status'] = {'kind': 'Late', 'delayMinutes': 7};
      stopNamed(detail, 'Kandy')['expectedArrival'] = '2026-10-20T03:52:00Z';
      await pumpTracking(tester, backendWith(detail));
      await tester.tap(find.text("I'm on this train"));
      await tester.pump();

      expect(find.text('09:22'), findsOneWidget);
      expect(find.textContaining('Scheduled', findRichText: true), findsOneWidget);
      expect(find.text('7 min late'), findsOneWidget);
    });

    trackingTest('the train has reached the destination: "Arrived at Kandy"', (tester) async {
      final detail = detailJson();
      final k = stopNamed(detail, 'Kandy');
      k['status'] = 'Arrived';
      k['actualArrival'] = '2026-10-20T03:46:00Z';
      await pumpTracking(tester, backendWith(detail));
      await tester.tap(find.text("I'm on this train"));
      await tester.pump();

      expect(find.text('Arrived at Kandy'), findsOneWidget);
      expect(find.text('09:16'), findsOneWidget);
    });

    trackingTest('the passenger can switch back to waiting', (tester) async {
      await pumpTracking(tester, backendWith(detailJson()));
      await tester.tap(find.text("I'm on this train"));
      await tester.pump();

      await tester.tap(find.text('Waiting'));
      await tester.pump();

      expect(find.text('Expected at Gampaha'), findsOneWidget);
      expect(find.text("I'm on this train"), findsOneWidget);
    });

    trackingTest('"Stop tracking" leaves the tracking screen', (tester) async {
      final backend = backendWith(detailJson());
      tester.view.physicalSize = const Size(390 * 2, 1700 * 2);
      tester.view.devicePixelRatio = 2;
      addTearDown(tester.view.reset);
      final controller = DetailController(
        SmartRailApi(backend.client, 'http://test'),
        journeyId,
        boardingStationId: gampahaId,
        destinationStationId: kandyId,
        newStopwatch: FakeStopwatch.new,
      );
      controllers.add(controller);
      unawaited(controller.load());
      await settle(tester);
      await tester.pumpWidget(
        MaterialApp(
          theme: buildPassengerTheme(Brightness.light),
          home: Builder(
            builder: (context) => Scaffold(
              body: TextButton(
                onPressed: () => Navigator.of(context).push(
                  MaterialPageRoute<void>(builder: (_) => TrackingScreen(controller: controller, onOpenJourney: (_) {})),
                ),
                child: const Text('open'),
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();
      await tester.tap(find.text("I'm on this train"));
      await tester.pump();

      await tester.tap(find.text('Stop tracking'));
      await tester.pumpAndSettle();

      expect(find.text('open'), findsOneWidget);
      expect(find.byType(TrackingScreen), findsNothing);
    });

    trackingTest('buttons are at least 48 px high', (tester) async {
      await pumpTracking(tester, backendWith(detailJson()));

      expect(tester.getSize(find.widgetWithText(FilledButton, "I'm on this train")).height, greaterThanOrEqualTo(48));
    });
  });

  group('"Are you on board?"', () {
    trackingTest('is asked when the train has left the boarding station: where it left and when', (tester) async {
      await pumpTracking(tester, backendWith(gampahaDeparted()));

      expect(find.text('Are you on board?'), findsOneWidget);
      expect(find.text('Kandy Express left Gampaha at 07:12'), findsOneWidget, reason: '01:42:30 UTC in Colombo time');
      expect(find.text("Yes, I'm on board"), findsOneWidget);
      expect(find.text('No, I missed it'), findsOneWidget);
    });

    trackingTest('is not asked while the train has not left', (tester) async {
      await pumpTracking(tester, backendWith(detailJson()));

      expect(find.text('Are you on board?'), findsNothing);
    });

    trackingTest('"Yes" switches to on board', (tester) async {
      await pumpTracking(tester, backendWith(gampahaDeparted()));

      await tester.tap(find.text("Yes, I'm on board"));
      await tester.pumpAndSettle();

      expect(find.text('Are you on board?'), findsNothing);
      expect(find.text('Next stop'), findsOneWidget);
      expect(find.text('Stop tracking'), findsOneWidget);
    });

    trackingTest('"No, I missed it" shows the next 3 trains to the same destination, from the search', (tester) async {
      final backend = backendWith(gampahaDeparted())
        ..journeys = [
          journeyJson(id: 'a', serviceNumber: '1003', name: 'Podi Menike'),
          journeyJson(id: 'b', serviceNumber: '1005', name: 'Udarata Menike'),
          journeyJson(id: 'c', serviceNumber: '1007', name: 'Night Mail'),
        ];
      await pumpTracking(tester, backend);

      await tester.tap(find.text('No, I missed it'));
      await tester.pumpAndSettle();

      expect(find.text('Are you on board?'), findsNothing);
      expect(find.text('Expected at Gampaha').evaluate().length + find.text('Left Gampaha').evaluate().length, 1, reason: 'still in waiting mode');
      expect(find.text('Next trains'), findsOneWidget);
      expect(find.textContaining('1003 Podi Menike', findRichText: true), findsOneWidget);
      expect(find.textContaining('1007 Night Mail', findRichText: true), findsOneWidget);

      final q = backend.requests.where((u) => u.path == '/api/v1/journeys/search').single.queryParameters;
      expect(q['from'], gampahaId);
      expect(q['to'], kandyId);
      expect(q['date'], '2026-10-20');
      expect(q['after'], '2026-10-20T01:42:00.000Z', reason: 'the scheduled departure of this train at Gampaha');
      expect(q['limit'], '3');
    });

    trackingTest('the missed train itself is not offered, and a tap opens one of the others', (tester) async {
      final opened = <JourneySummary>[];
      final backend = backendWith(gampahaDeparted())
        ..journeys = [
          journeyJson(id: journeyId, serviceNumber: '1001', name: 'Kandy Express'),
          journeyJson(id: 'a', serviceNumber: '1003', name: 'Podi Menike'),
        ];
      await pumpTracking(tester, backend, opened: opened);
      await tester.tap(find.text('No, I missed it'));
      await tester.pumpAndSettle();

      expect(find.textContaining('1001 Kandy Express', findRichText: true), findsNothing);
      await tester.tap(find.textContaining('1003 Podi Menike', findRichText: true));
      expect(opened.single.journeyId, 'a');
    });

    trackingTest('dismissing the question changes nothing and it is not asked again', (tester) async {
      final backend = backendWith(gampahaDeparted());
      final controller = await pumpTracking(tester, backend);

      // Tap outside the sheet: it closes without an answer.
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();
      expect(find.text('Are you on board?'), findsNothing);
      expect(find.text('Expected at Gampaha').evaluate().length + find.text('Left Gampaha').evaluate().length, 1);
      expect(controller.missed, isFalse);

      unawaited(controller.refresh());
      await settle(tester, const Duration(seconds: 2));
      expect(find.text('Are you on board?'), findsNothing, reason: 'asked once per visit');
    });

    trackingTest('a passenger who is already on board is not asked', (tester) async {
      final backend = backendWith(detailJson());
      final controller = await pumpTracking(tester, backend);
      await tester.tap(find.text("I'm on this train"));
      await tester.pump();

      backend.detail = gampahaDeparted();
      unawaited(controller.refresh());
      await settle(tester, const Duration(seconds: 1));

      expect(find.text('Are you on board?'), findsNothing);
    });

    trackingTest('asked as soon as the train leaves while the screen is open (live)', (tester) async {
      final backend = backendWith(detailJson());
      await pumpTracking(tester, backend);
      expect(find.text('Are you on board?'), findsNothing);

      // The snapshot of the stream carries the journey, now with Gampaha left.
      backend.streams.last.frame(
        id: '300',
        event: 'snapshot',
        data: {'journeyId': journeyId, 'sequence': 300, 'journey': gampahaDeparted()},
      );
      await settle(tester, const Duration(seconds: 1));

      expect(find.text('Are you on board?'), findsOneWidget);
    });

    trackingTest('not asked for a cancelled train (the boarding stop is skipped, not left)', (tester) async {
      final detail = detailJson()..['status'] = {'kind': 'Cancelled', 'delayMinutes': null};
      for (final name in ['Gampaha', 'Polgahawela', 'Peradeniya', 'Kandy']) {
        stopNamed(detail, name)['status'] = 'Skipped';
      }
      await pumpTracking(tester, backendWith(detail)..journeys = []);

      expect(find.text('Are you on board?'), findsNothing);
    });
  });

  group('"I missed it" in the controller', () {
    test('no search before the passenger says so', () async {
      final backend = backendWith(gampahaDeparted());
      final c = DetailController(SmartRailApi(backend.client, 'http://test'), journeyId, boardingStationId: gampahaId, destinationStationId: kandyId);
      await c.load();
      c.stopLive();

      expect(c.missed, isFalse);
      expect(c.alternatives, isA<NoAlternatives>());
      expect(backend.requests.where((u) => u.path == '/api/v1/journeys/search'), isEmpty);
    });
  });
}
