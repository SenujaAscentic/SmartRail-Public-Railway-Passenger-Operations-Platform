import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';
import 'package:smartrail_passenger/features/details/details_controller.dart';
import 'package:smartrail_passenger/features/details/details_screen.dart';

import '../../support/fake_backend.dart';

/// A stopwatch the test moves by hand (the real one ignores the test's clock).
class FakeStopwatch extends Stopwatch {
  Duration passed = Duration.zero;

  @override
  Duration get elapsed => passed;

  @override
  void start() {}
}

// The fixture answer is from 20 Oct 2026, data updated at 01:16:31.183576 UTC.
const updatedAt = '2026-10-20T01:16:31.183576Z';
// The Date header has whole seconds: 30.8 s after updatedAt, shown rounded down as 30 s.
const thirtySecondsLater = 'Tue, 20 Oct 2026 01:17:02 GMT';

Future<FakeStopwatch> pumpDetails(
  WidgetTester tester,
  FakeBackend backend, {
  String? boarding,
  String? destination,
  Brightness brightness = Brightness.light,
  List<JourneySummary>? opened,
}) async {
  tester.view.physicalSize = const Size(390 * 2, 1600 * 2);
  tester.view.devicePixelRatio = 2;
  addTearDown(tester.view.reset);
  final watch = FakeStopwatch();
  final controller = DetailController(
    SmartRailApi(backend.client, 'http://test'),
    'journey-1',
    boardingStationId: boarding,
    destinationStationId: destination,
    newStopwatch: () => watch,
  );
  await tester.pumpWidget(
    MaterialApp(
      theme: buildPassengerTheme(brightness),
      home: DetailsScreen(controller: controller, onOpenJourney: (j) => opened?.add(j)),
    ),
  );
  await tester.pumpAndSettle();
  return watch;
}

FakeBackend backendWith(Map<String, dynamic> detail, {String? date = thirtySecondsLater}) =>
    FakeBackend()
      ..detail = detail
      ..dateHeader = date;

List<StopState> rowStates(WidgetTester tester) => [for (final r in tester.widgetList<StopRow>(find.byType(StopRow))) r.state];

void main() {
  setUpAll(loadAppFonts);

  testWidgets('header: name, train line, status and the request for the right journey', (tester) async {
    final backend = backendWith(detailJson());
    await pumpDetails(tester, backend);

    expect(find.text('Kandy Express'), findsOneWidget);
    expect(find.text('Train 1001 · Colombo Fort → Kandy'), findsOneWidget);
    expect(find.text('On time'), findsOneWidget);
    expect(backend.requests.single.path, '/api/v1/journeys/journey-1');
  });

  testWidgets('the stop list has calling points only, in order', (tester) async {
    await pumpDetails(tester, backendWith(detailJson()));

    final names = [for (final r in tester.widgetList<StopRow>(find.byType(StopRow))) r.name];
    expect(names, ['Colombo Fort', 'Maradana', 'Gampaha', 'Polgahawela', 'Peradeniya', 'Kandy']);
    // (Kelaniya and Ragama still appear in "Between Kelaniya and Ragama": that is the
    // position of the train, not the stop list.)
    for (final passedThrough in ['Kelaniya', 'Ragama', 'Veyangoda', 'Rambukkana', 'Kadugannawa']) {
      expect(names, isNot(contains(passedThrough)), reason: passedThrough);
    }
  });

  testWidgets('stops the train has left are marked, the others are not', (tester) async {
    await pumpDetails(tester, backendWith(detailJson()));

    expect(rowStates(tester), [
      StopState.departed,
      StopState.departed,
      StopState.scheduled,
      StopState.scheduled,
      StopState.scheduled,
      StopState.scheduled,
    ]);
  });

  testWidgets('times are Colombo times: arrival, or departure at the passenger\'s boarding station', (tester) async {
    await pumpDetails(tester, backendWith(detailJson()), boarding: gampahaId);

    // Gampaha: scheduled departure 01:42 UTC = 07:12; the destination arrives 03:45 UTC = 09:15.
    expect(find.text('07:12'), findsOneWidget);
    expect(find.text('07:10'), findsNothing);
    expect(find.text('09:15'), findsOneWidget);
    // The origin has no arrival, so its departure shows: 01:00 UTC = 06:30.
    expect(find.text('06:30'), findsOneWidget);
  });

  testWidgets('without a boarding station every stop shows its arrival', (tester) async {
    await pumpDetails(tester, backendWith(detailJson()));

    expect(find.text('07:10'), findsOneWidget); // Gampaha arrival 01:40 UTC
  });

  testWidgets('a stop the train has left shows the actual time, struck-through schedule when it differs', (tester) async {
    final detail = detailJson();
    final maradana = stopNamed(detail, 'Maradana');
    maradana['scheduledArrival'] = '2026-10-20T01:05:00Z';
    maradana['actualArrival'] = '2026-10-20T01:09:00Z'; // 4 min after schedule
    maradana['expectedArrival'] = '2026-10-20T01:09:00Z';
    await pumpDetails(tester, backendWith(detail));

    expect(find.text('06:39'), findsOneWidget);
    expect(tester.widget<Text>(find.text('06:35')).style!.decoration, TextDecoration.lineThrough);
  });

  testWidgets('"Your station" with its platform, and the destination', (tester) async {
    await pumpDetails(tester, backendWith(detailJson()), boarding: gampahaId, destination: kandyId);

    expect(find.text('Your station · Platform 1'), findsOneWidget);
    expect(find.text('destination'), findsOneWidget);
  });

  testWidgets('a changed platform reads "Platform 5 (was 2)" on any stop', (tester) async {
    final detail = detailJson();
    final polgahawela = stopNamed(detail, 'Polgahawela');
    polgahawela['platform'] = '5';
    polgahawela['previousPlatform'] = '2';
    await pumpDetails(tester, backendWith(detail));

    expect(find.text('Platform 5 (was 2)'), findsOneWidget);
  });

  testWidgets('a skipped stop is listed with "Does not stop"', (tester) async {
    final detail = detailJson();
    stopNamed(detail, 'Gampaha')['status'] = 'Skipped';
    await pumpDetails(tester, backendWith(detail));

    expect(find.text('Does not stop'), findsOneWidget);
    expect(rowStates(tester)[2], StopState.skipped);
  });

  group('where the train is (lastPosition from the backend, never computed)', () {
    testWidgets('between two stations: chip text, and the diagram gets the backend\'s fraction', (tester) async {
      await pumpDetails(tester, backendWith(detailJson()));

      expect(find.text('Between Kelaniya and Ragama'), findsOneWidget);
      final diagram = tester.widget<LineDiagram>(find.byType(LineDiagram));
      expect(diagram.from, 'Kelaniya');
      expect(diagram.to, 'Ragama');
      expect(diagram.progress, closeTo(0.2933105234722473, 1e-9));
    });

    testWidgets('a changed fraction moves the marker', (tester) async {
      final detail = detailJson();
      ((detail['freshness'] as Map)['lastPosition'] as Map)['fractionToNext'] = 0.8;
      await pumpDetails(tester, backendWith(detail));

      expect(tester.widget<LineDiagram>(find.byType(LineDiagram)).progress, 0.8);
    });

    testWidgets('at a station: "At Gampaha" with the neighbouring calling points', (tester) async {
      final detail = detailJson();
      stopNamed(detail, 'Gampaha')['status'] = 'Arrived';
      await pumpDetails(tester, backendWith(detail));

      expect(find.text('At Gampaha'), findsOneWidget);
      final diagram = tester.widget<LineDiagram>(find.byType(LineDiagram));
      expect(diagram.previous, 'Maradana');
      expect(diagram.next, 'Polgahawela');
    });

    testWidgets('lost data: no position at all, only the banner', (tester) async {
      final detail = detailJson();
      (detail['freshness'] as Map)['tracking'] = 'Lost';
      await pumpDetails(tester, backendWith(detail));

      expect(find.byType(LineDiagram), findsNothing);
      expect(find.textContaining('Between'), findsNothing);
      expect(find.text('Live location unavailable – scheduled times shown'), findsOneWidget);
    });

    testWidgets('stale data: the last position stays, with "Last seen 06:46 near Kelaniya"', (tester) async {
      final detail = detailJson();
      (detail['freshness'] as Map)['tracking'] = 'Stale';
      await pumpDetails(tester, backendWith(detail));

      expect(find.byType(LineDiagram), findsOneWidget);
      expect(find.text('Last seen 06:46 near Kelaniya'), findsOneWidget);
    });

    testWidgets('not started: no position', (tester) async {
      final detail = detailJson();
      (detail['freshness'] as Map)
        ..['tracking'] = 'NotStarted'
        ..['lastPosition'] = null
        ..['updatedAt'] = null;
      await pumpDetails(tester, backendWith(detail));

      expect(find.byType(LineDiagram), findsNothing);
    });

    testWidgets('a fraction outside 0 to 1 is not drawn', (tester) async {
      final detail = detailJson();
      ((detail['freshness'] as Map)['lastPosition'] as Map)['fractionToNext'] = 1.7;
      await pumpDetails(tester, backendWith(detail));

      expect(find.byType(LineDiagram), findsNothing);
    });
  });

  group('age of the data', () {
    testWidgets('server time minus updatedAt, then it counts on with the stopwatch', (tester) async {
      final watch = await pumpDetails(tester, backendWith(detailJson()));
      expect(find.text('Updated 30 s ago'), findsOneWidget);

      watch.passed = const Duration(seconds: 45);
      await tester.pump(const Duration(seconds: 1));
      expect(find.text('Updated 1 min ago'), findsOneWidget);
    });

    testWidgets('no Date header: no age is shown', (tester) async {
      await pumpDetails(tester, backendWith(detailJson(), date: null));
      expect(find.textContaining('Updated'), findsNothing);
    });

    testWidgets('a Date header before updatedAt counts as age zero', (tester) async {
      await pumpDetails(tester, backendWith(detailJson(), date: 'Tue, 20 Oct 2026 01:10:00 GMT'));
      expect(find.text('Updated 0 s ago'), findsOneWidget);
    });
  });

  group('train set', () {
    testWidgets('traction and each class with its seating', (tester) async {
      await pumpDetails(tester, backendWith(detailJson()));

      expect(find.textContaining('Diesel power set', findRichText: true), findsOneWidget);
      expect(find.textContaining('1st class · reserved', findRichText: true), findsOneWidget);
      expect(find.textContaining('2nd class · reserved', findRichText: true), findsOneWidget);
      expect(find.textContaining('3rd class · unreserved', findRichText: true), findsOneWidget);
    });

    testWidgets('locomotive-hauled is written out', (tester) async {
      final detail = detailJson();
      (detail['trainSet'] as Map)['traction'] = 'DieselLocoHauled';
      await pumpDetails(tester, backendWith(detail));

      expect(find.textContaining('Diesel locomotive-hauled', findRichText: true), findsOneWidget);
    });

    testWidgets('no train set yet: "Usually: power set"', (tester) async {
      final detail = detailJson()..['trainSet'] = null;
      await pumpDetails(tester, backendWith(detail));

      expect(find.text('Usually: power set'), findsOneWidget);
    });
  });

  group('refresh and errors', () {
    testWidgets('the refresh button loads the journey again and shows the new status', (tester) async {
      final backend = backendWith(detailJson());
      await pumpDetails(tester, backend);
      expect(find.text('On time'), findsOneWidget);

      final later = detailJson();
      later['status'] = {'kind': 'Late', 'delayMinutes': 4};
      backend.detail = later;
      await tester.tap(find.byTooltip('Refresh'));
      await tester.pumpAndSettle();

      expect(find.text('4 min late'), findsOneWidget);
      expect(find.text('On time'), findsNothing);
      expect(backend.requests, hasLength(2));
    });

    testWidgets('a failed refresh keeps the old data and says it is old', (tester) async {
      final backend = backendWith(detailJson());
      final watch = await pumpDetails(tester, backend);
      watch.passed = const Duration(minutes: 4);

      backend.failJourney = true;
      await tester.tap(find.byTooltip('Refresh'));
      await tester.pumpAndSettle();

      expect(find.text('Kandy Express'), findsOneWidget);
      expect(find.text("You're offline · updated 4 min ago"), findsOneWidget);
    });

    testWidgets('the first load failing shows the error, and "Try again" works', (tester) async {
      final backend = backendWith(detailJson())..failJourney = true;
      await pumpDetails(tester, backend);
      expect(find.text("Couldn't load train information"), findsOneWidget);
      expect(find.text('Kandy Express'), findsNothing);

      backend.failJourney = false;
      await tester.tap(find.text('Try again'));
      await tester.pumpAndSettle();

      expect(find.text('Kandy Express'), findsOneWidget);
    });

    testWidgets('an unknown journey (404) shows the same error', (tester) async {
      await pumpDetails(tester, FakeBackend()); // detail is null: 404
      expect(find.text("Couldn't load train information"), findsOneWidget);
    });
  });

  testWidgets('dark theme: the same content with token colours', (tester) async {
    await pumpDetails(tester, backendWith(detailJson()), brightness: Brightness.dark);

    expect(find.text('Kandy Express'), findsOneWidget);
    final badge = tester.widget<Text>(find.text('On time'));
    expect(badge.style!.color, tokenFg('OnTime', dark: true));
  });
}

Color tokenFg(String name, {required bool dark}) => dark ? statusTokens[name]!.dark.fg : statusTokens[name]!.light.fg;
