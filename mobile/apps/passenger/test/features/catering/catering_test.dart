import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';
import 'package:smartrail_passenger/features/details/details_controller.dart';
import 'package:smartrail_passenger/features/details/details_screen.dart';

import '../../support/fake_backend.dart';

// US-P8: "Food available at ..." on the train details, the menu preview, "Call to order", opening hours, and the provider being
// down. The journey is the real answer of train 1001 (Colombo Fort to Kandy); the passenger rides Gampaha to Kandy, so the
// stops between are Polgahawela and Peradeniya (Veyangoda, Rambukkana and Kadugannawa are only passed).

// Server times (Date header) on 20 Oct 2026. Colombo is UTC+05:30.
const midday = 'Tue, 20 Oct 2026 06:30:00 GMT'; // 12:00 in Colombo: the hotline is open (06:00 to 20:00)
const evening = 'Tue, 20 Oct 2026 15:30:00 GMT'; // 21:00: closed
const beforeOpening = 'Tue, 20 Oct 2026 00:00:00 GMT'; // 05:30: closed, opens 06:00

FakeBackend backendWithFood({bool polgahawela = true, bool peradeniya = false}) {
  return FakeBackend()
    ..detail = detailJson()
    ..dateHeader = 'Tue, 20 Oct 2026 01:17:02 GMT'
    ..cateringStations.addAll({polgahawelaId: polgahawela, peradeniyaId: peradeniya})
    ..catering = cateringJson()
    ..cateringDateHeader = midday;
}

/// The details screen for a passenger riding from [boarding] to [destination] (null: the stations are not known).
Future<List<Uri>> pumpDetails(
  WidgetTester tester,
  FakeBackend backend, {
  String? boarding = gampahaId,
  String? destination = kandyId,
  Future<bool> Function(Uri)? launch,
  List<Uri>? launched,
}) async {
  tester.view.physicalSize = const Size(390 * 2, 1800 * 2);
  tester.view.devicePixelRatio = 2;
  addTearDown(tester.view.reset);
  final calls = launched ?? <Uri>[];
  final controller = DetailController(
    SmartRailApi(backend.client, 'http://test'),
    'journey-1',
    boardingStationId: boarding,
    destinationStationId: destination,
  );
  await tester.pumpWidget(
    MaterialApp(
      theme: buildPassengerTheme(Brightness.light),
      home: DetailsScreen(
        controller: controller,
        onOpenJourney: (_) {},
        launchPhone: launch ??
            (uri) async {
              calls.add(uri);
              return true;
            },
        nowUtc: () => DateTime.utc(2026, 10, 20, 6, 30),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return calls;
}

Future<void> openMenu(WidgetTester tester) async {
  await tester.tap(find.text(Strings.foodAvailableAt('Polgahawela')));
  await tester.pumpAndSettle();
}

void main() {
  group('Food available at ... (AC1)', () {
    testWidgets('shows at a stop between boarding and destination that has catering, and only there', (tester) async {
      await pumpDetails(tester, backendWithFood());

      expect(find.text(Strings.foodAvailableAt('Polgahawela')), findsOneWidget);
      expect(find.text(Strings.foodAvailableAt('Peradeniya')), findsNothing); // no catering there
      expect(find.text(Strings.foodAvailableAt('Kandy')), findsNothing);
    });

    testWidgets('not at the boarding station or the destination, even if they have catering', (tester) async {
      final backend = backendWithFood()..cateringStations.addAll({gampahaId: true, kandyId: true});
      await pumpDetails(tester, backend);

      expect(find.text(Strings.foodAvailableAt('Gampaha')), findsNothing);
      expect(find.text(Strings.foodAvailableAt('Kandy')), findsNothing);
      expect(find.text(Strings.foodAvailableAt('Polgahawela')), findsOneWidget);
    });

    testWidgets('not at a stop the train only passes (Veyangoda is between, but the train does not stop)', (tester) async {
      final backend = backendWithFood()..cateringStations['01a14eb2-015b-79cf-9e6c-ab14cf11a22a'] = true;
      await pumpDetails(tester, backend);

      expect(find.text(Strings.foodAvailableAt('Veyangoda')), findsNothing);
    });

    testWidgets("nothing when the passenger's stations are not known", (tester) async {
      final backend = backendWithFood();
      await pumpDetails(tester, backend, boarding: null, destination: null);

      expect(find.textContaining('Food available at'), findsNothing);
      expect(backend.requests.where((u) => u.path.startsWith('/api/v1/stations/')), isEmpty);
    });

    testWidgets('a station that cannot be read is left out; all train information keeps working', (tester) async {
      final backend = backendWithFood()..failStations.add(polgahawelaId);
      await pumpDetails(tester, backend);

      expect(find.textContaining('Food available at'), findsNothing);
      expect(find.text('Kandy Express'), findsOneWidget);
      expect(find.text('Polgahawela'), findsOneWidget); // still in the stop list
    });

    testWidgets('the stations are asked about once, not on every redraw', (tester) async {
      final backend = backendWithFood();
      await pumpDetails(tester, backend);
      await tester.pump(const Duration(seconds: 3)); // the screen redraws every second

      expect(backend.requests.where((u) => u.path == '/api/v1/stations/$polgahawelaId').length, 1);
    });
  });

  group('the menu preview (AC2)', () {
    testWidgets('tapping the line shows items, prices and available / sold out', (tester) async {
      await pumpDetails(tester, backendWithFood());
      await openMenu(tester);

      expect(find.text(Strings.foodAt('Polgahawela')), findsOneWidget);
      expect(find.text('Chicken rice'), findsOneWidget);
      expect(find.text('Rs 450'), findsOneWidget);
      expect(find.text('Egg roti'), findsOneWidget);
      expect(find.text('Rs 120.50'), findsOneWidget);
      // Words with an icon, never colour alone.
      expect(find.text(Strings.available), findsOneWidget);
      expect(find.text(Strings.soldOut), findsOneWidget);
      expect(find.byIcon(Icons.check_circle_outline), findsOneWidget);
      expect(find.byIcon(Icons.block), findsOneWidget);
    });

    testWidgets('shows the two labels: in-app ordering is coming, orders are handled by the provider (AC5)', (tester) async {
      await pumpDetails(tester, backendWithFood());
      await openMenu(tester);

      expect(find.text('In-app ordering coming soon'), findsOneWidget);
      expect(find.text('Orders are handled by RailBites Catering'), findsOneWidget);
    });
  });

  group('Call to order (AC3, AC4)', () {
    testWidgets('opens the dialler with the hotline as a tel: address', (tester) async {
      final launched = <Uri>[];
      await pumpDetails(tester, backendWithFood(), launched: launched);
      await openMenu(tester);

      await tester.tap(find.text(Strings.callToOrder));
      await tester.pumpAndSettle();

      expect(launched, hasLength(1));
      expect(launched.single.scheme, 'tel');
      expect(launched.single.path, '+94112345678');
    });

    testWidgets("says so when the phone can't open the dialler", (tester) async {
      await pumpDetails(tester, backendWithFood(), launch: (_) async => false);
      await openMenu(tester);

      await tester.tap(find.text(Strings.callToOrder));
      await tester.pumpAndSettle();

      expect(find.text(Strings.couldNotCall), findsOneWidget);
    });

    testWidgets('shows the opening hours; open now, so no closed message', (tester) async {
      await pumpDetails(tester, backendWithFood());
      await openMenu(tester);

      expect(find.text('Opening hours 06:00–20:00'), findsOneWidget);
      expect(find.textContaining('Hotline closed'), findsNothing);
    });

    testWidgets('outside the hours: "Hotline closed – opens 06:00" (evening)', (tester) async {
      await pumpDetails(tester, backendWithFood()..cateringDateHeader = evening);
      await openMenu(tester);

      expect(find.text('Hotline closed – opens 06:00'), findsOneWidget);
      expect(find.text('Opening hours 06:00–20:00'), findsOneWidget);
      expect(find.byIcon(Icons.schedule), findsOneWidget); // icon + words
    });

    testWidgets('outside the hours: before opening in the morning', (tester) async {
      await pumpDetails(tester, backendWithFood()..cateringDateHeader = beforeOpening);
      await openMenu(tester);

      expect(find.text('Hotline closed – opens 06:00'), findsOneWidget);
    });

    testWidgets('a closed hotline can still be called (a wrong clock must not block a call)', (tester) async {
      final launched = <Uri>[];
      await pumpDetails(tester, backendWithFood()..cateringDateHeader = evening, launched: launched);
      await openMenu(tester);

      await tester.tap(find.text(Strings.callToOrder));
      await tester.pumpAndSettle();

      expect(launched, hasLength(1));
    });

    testWidgets('without a Date header the phone clock decides', (tester) async {
      // The phone clock in this test is 06:30 UTC = 12:00 in Colombo: open.
      await pumpDetails(tester, backendWithFood()..cateringDateHeader = null);
      await openMenu(tester);

      expect(find.textContaining('Hotline closed'), findsNothing);
    });

    testWidgets('shows what to say on the phone: train, arrival time, coach', (tester) async {
      final backend = backendWithFood();
      await pumpDetails(tester, backend);
      await openMenu(tester);

      final stop = stopNamed(backend.detail!, 'Polgahawela');
      final arrival = DateTime.parse((stop['expectedArrival'] ?? stop['scheduledArrival']) as String);
      expect(find.text('Mention: Train 1001 · arriving Polgahawela ~${formatTime(arrival)} · your coach'), findsOneWidget);
    });
  });

  group('the provider is down (AC6)', () {
    testWidgets('"Menu unavailable" when the provider says it is not available', (tester) async {
      await pumpDetails(tester, backendWithFood()..catering = cateringJson(available: false, hotline: null, openingHours: null, menu: []));
      await openMenu(tester);

      expect(find.text(Strings.menuUnavailable), findsOneWidget);
      expect(find.text(Strings.callToOrder), findsNothing);
    });

    testWidgets('"Menu unavailable" when the request fails', (tester) async {
      await pumpDetails(tester, backendWithFood()..failCatering = true);
      await openMenu(tester);

      expect(find.text(Strings.menuUnavailable), findsOneWidget);
    });

    testWidgets('"Menu unavailable" when there is no menu in the cache', (tester) async {
      await pumpDetails(tester, backendWithFood()..catering = cateringJson(menu: []));
      await openMenu(tester);

      expect(find.text(Strings.menuUnavailable), findsOneWidget);
    });

    testWidgets('Try again loads it once the provider is back', (tester) async {
      final backend = backendWithFood()..failCatering = true;
      await pumpDetails(tester, backend);
      await openMenu(tester);
      expect(find.text(Strings.menuUnavailable), findsOneWidget);

      backend.failCatering = false;
      await tester.tap(find.text(Strings.tryAgain));
      await tester.pumpAndSettle();

      expect(find.text('Chicken rice'), findsOneWidget);
    });

    testWidgets('all train information keeps working: going back, the train details are unchanged', (tester) async {
      await pumpDetails(tester, backendWithFood()..failCatering = true);
      await openMenu(tester);
      expect(find.text(Strings.menuUnavailable), findsOneWidget);

      await tester.pageBack();
      await tester.pumpAndSettle();

      expect(find.text('Kandy Express'), findsOneWidget);
      expect(find.text(Strings.foodAvailableAt('Polgahawela')), findsOneWidget);
    });
  });
}
