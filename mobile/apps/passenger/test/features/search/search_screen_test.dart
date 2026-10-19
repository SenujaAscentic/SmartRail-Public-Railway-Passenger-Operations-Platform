import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';
import 'package:smartrail_passenger/main.dart';

import '../../support/fake_backend.dart';

// 2026-10-13 20:00 UTC is already 14 Oct 01:30 in Colombo: a phone in UTC would
// say 13 Oct, so this "now" proves the date comes from the Colombo function.
final fixedNow = DateTime.utc(2026, 10, 13, 20, 0);

Future<void> pumpApp(WidgetTester tester, FakeBackend backend) async {
  tester.view.physicalSize = const Size(390 * 2, 844 * 2);
  tester.view.devicePixelRatio = 2;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(PassengerApp(api: SmartRailApi(backend.client, 'http://test'), nowUtc: () => fixedNow));
}

/// Types [text] into the field with [label] and picks the suggestion [name].
Future<void> pickStation(WidgetTester tester, String label, String text, String name) async {
  await tester.enterText(find.widgetWithText(TextField, label), text);
  await tester.pump(const Duration(milliseconds: 400)); // typing pause
  await tester.pump(); // answer arrives
  await tester.tap(find.text(name).last);
  await tester.pump();
}

Future<void> searchGampahaKandy(WidgetTester tester) async {
  await pickStation(tester, Strings.from, 'Gam', 'Gampaha');
  await pickStation(tester, Strings.to, 'Kan', 'Kandy');
  await tester.tap(find.text(Strings.search));
  await tester.pumpAndSettle();
}

void main() {
  setUpAll(loadAppFonts);

  testWidgets('the form starts with today in Colombo and Search disabled', (tester) async {
    await pumpApp(tester, FakeBackend());

    expect(find.text('Date: 14 Oct 2026'), findsOneWidget);
    expect(tester.widget<FilledButton>(find.byType(FilledButton)).onPressed, isNull);
    expect(find.text(Strings.noDirectTrains), findsNothing);
  });

  testWidgets('type-ahead offers matching stations after a pause', (tester) async {
    final backend = FakeBackend();
    await pumpApp(tester, backend);

    await tester.enterText(find.widgetWithText(TextField, Strings.from), 'kan');
    await tester.pump(const Duration(milliseconds: 400));
    await tester.pump();

    expect(find.text('Kandy'), findsOneWidget);
    expect(find.text('Gampaha'), findsNothing);
    expect(backend.requests.single.queryParameters, {'query': 'kan'});
  });

  testWidgets('typing quickly asks the backend once, for the final text', (tester) async {
    final backend = FakeBackend();
    await pumpApp(tester, backend);
    final field = find.widgetWithText(TextField, Strings.from);

    await tester.enterText(field, 'k');
    await tester.pump(const Duration(milliseconds: 100));
    await tester.enterText(field, 'ka');
    await tester.pump(const Duration(milliseconds: 100));
    await tester.enterText(field, 'kan');
    await tester.pump(const Duration(milliseconds: 400));
    await tester.pump();

    expect(backend.requests.map((u) => u.queryParameters['query']), ['kan']);
  });

  testWidgets('search sends the chosen station ids and date, then shows the results with Colombo times', (tester) async {
    final backend = FakeBackend()
      ..journeys = [
        journeyJson(
          status: {'kind': 'Late', 'delayMinutes': 7},
          scheduledDeparture: '2026-10-14T11:32:00Z',
          expectedDeparture: '2026-10-14T11:39:00Z',
          scheduledArrival: '2026-10-14T13:35:00Z',
          expectedArrival: '2026-10-14T13:42:00Z',
        ),
        journeyJson(id: 'b', serviceNumber: '1003', name: 'Podi Menike', scheduledDeparture: '2026-10-14T12:30:00Z', expectedDeparture: '2026-10-14T12:30:00Z', scheduledArrival: '2026-10-14T14:40:00Z', expectedArrival: '2026-10-14T14:40:00Z', platform: '1'),
      ];
    await pumpApp(tester, backend);

    await searchGampahaKandy(tester);

    final search = backend.requests.last;
    expect(search.path, '/api/v1/journeys/search');
    expect(search.queryParameters, {
      'from': '9a4e2d71-5b38-4c06-8e1f-6a0d3b7c9e24',
      'to': 'c7d05f38-1e92-4ab6-9d70-4f8a2c1e6b35',
      'date': '2026-10-14',
    });
    // 11:39 UTC = 17:09 and 13:42 UTC = 19:12 in Colombo.
    expect(find.text('17:09 → 19:12'), findsOneWidget);
    expect(find.text('17:02 → 19:05 scheduled'), findsOneWidget);
    expect(find.text('7 min late'), findsOneWidget);
    expect(find.text('Platform 2'), findsOneWidget);
    expect(find.textContaining('1001 Kandy Express', findRichText: true), findsOneWidget);
    // Second result keeps the backend's order and shows "On time".
    expect(find.text('18:00 → 20:10'), findsOneWidget);
    expect(find.text('On time'), findsOneWidget);
  });

  testWidgets('a cancelled train is listed with "Cancelled"', (tester) async {
    final backend = FakeBackend()
      ..journeys = [journeyJson(status: {'kind': 'Cancelled', 'delayMinutes': null}, expectedDeparture: null, expectedArrival: null, platform: '')];
    await pumpApp(tester, backend);

    await searchGampahaKandy(tester);

    expect(find.text('Cancelled'), findsOneWidget);
    expect(find.textContaining('Platform'), findsNothing);
  });

  testWidgets('a platform change reads "Platform 5 (was 2)"', (tester) async {
    final backend = FakeBackend()..journeys = [journeyJson(platform: '5', previousPlatform: '2')];
    await pumpApp(tester, backend);

    await searchGampahaKandy(tester);

    expect(find.text('Platform 5 (was 2)'), findsOneWidget);
  });

  testWidgets('no direct trains: the empty-state text', (tester) async {
    await pumpApp(tester, FakeBackend());

    await searchGampahaKandy(tester);

    expect(find.text('No direct trains between these stations on this date'), findsOneWidget);
  });

  testWidgets('a failed search shows the error and "Try again" works', (tester) async {
    final backend = FakeBackend()
      ..failJourneySearch = true
      ..journeys = [journeyJson()];
    await pumpApp(tester, backend);

    await searchGampahaKandy(tester);
    expect(find.text("Couldn't load train information"), findsOneWidget);

    backend.failJourneySearch = false;
    await tester.tap(find.text('Try again'));
    await tester.pumpAndSettle();

    expect(find.text("Couldn't load train information"), findsNothing);
    expect(find.text('On time'), findsOneWidget);
  });

  testWidgets('changing a station clears the old results', (tester) async {
    final backend = FakeBackend()..journeys = [journeyJson()];
    await pumpApp(tester, backend);
    await searchGampahaKandy(tester);
    expect(find.text('On time'), findsOneWidget);

    await tester.enterText(find.widgetWithText(TextField, Strings.to), 'Kan');
    await tester.pump(const Duration(milliseconds: 400));
    await tester.pump();

    expect(find.text('On time'), findsNothing);
    expect(tester.widget<FilledButton>(find.byType(FilledButton)).onPressed, isNull);
  });

  testWidgets('the same station twice cannot be searched', (tester) async {
    await pumpApp(tester, FakeBackend());

    await pickStation(tester, Strings.from, 'Kan', 'Kandy');
    await pickStation(tester, Strings.to, 'Kan', 'Kandy');

    expect(tester.widget<FilledButton>(find.byType(FilledButton)).onPressed, isNull);
  });

  testWidgets('another date can be chosen and is sent to the backend', (tester) async {
    final backend = FakeBackend();
    await pumpApp(tester, backend);

    await tester.tap(find.textContaining('Date:'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('20'));
    await tester.tap(find.text('OK'));
    await tester.pumpAndSettle();
    expect(find.text('Date: 20 Oct 2026'), findsOneWidget);

    await pickStation(tester, Strings.from, 'Gam', 'Gampaha');
    await pickStation(tester, Strings.to, 'Kan', 'Kandy');
    await tester.tap(find.text(Strings.search));
    await tester.pumpAndSettle();

    expect(backend.requests.last.queryParameters['date'], '2026-10-20');
  });

  testWidgets('days before today cannot be chosen', (tester) async {
    await pumpApp(tester, FakeBackend());

    await tester.tap(find.textContaining('Date:'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('13')); // yesterday in Colombo
    await tester.pump();
    await tester.tap(find.text('OK'));
    await tester.pumpAndSettle();

    expect(find.text('Date: 14 Oct 2026'), findsOneWidget);
  });

  testWidgets('tapping a result opens the train details of that journey, and back returns to the results', (tester) async {
    final backend = FakeBackend()
      ..journeys = [journeyJson(id: 'abc-123')]
      ..detail = detailJson();
    await pumpApp(tester, backend);
    await searchGampahaKandy(tester);

    await tester.tap(find.textContaining('1001 Kandy Express', findRichText: true));
    await tester.pumpAndSettle();

    expect(backend.requests.last.path, '/api/v1/journeys/abc-123');
    expect(find.text('Train 1001 · Colombo Fort → Kandy'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();
    expect(find.text('On time'), findsOneWidget);
    expect(find.text(Strings.search), findsOneWidget);
  });
}
