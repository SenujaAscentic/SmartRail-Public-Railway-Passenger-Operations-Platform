import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';
import 'package:smartrail_passenger/features/details/details_controller.dart';
import 'package:smartrail_passenger/features/details/details_screen.dart';

import '../../support/fake_backend.dart';

// US-P8: "Food available at ..." on the train details, the menu preview, "Call to order", opening hours, and the provider being
// down. The journey is the real answer of train 1001 (Colombo Fort to Kandy); the passenger rides Gampaha to Kandy, so the
// stops between are Polgahawela and Peradeniya (Veyangoda, Rambukkana and Kadugannawa are only passed).

FakeBackend backendWithFood({bool polgahawela = true, bool peradeniya = false}) {
  return FakeBackend()
    ..detail = detailJson()
    ..dateHeader = 'Tue, 20 Oct 2026 01:17:02 GMT'
    ..cateringStations.addAll({polgahawelaId: polgahawela, peradeniyaId: peradeniya})
    ..catering = cateringJson();
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
      ),
    ),
  );
  await tester.pumpAndSettle();
  return calls;
}

/// The food backend with another catering answer (open / closed now, hotline, hours).
FakeBackend backendWith({
  bool openNow = true,
  String hotline = '+94112345678',
  Map<String, String>? openingHours = const {'opens': '06:00', 'closes': '20:00'},
}) =>
    backendWithFood()..catering = cateringJson(openNow: openNow, hotline: hotline, openingHours: openingHours);

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
      expect(find.text('Rs 120'), findsOneWidget);
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

    testWidgets('shows the opening hours; open now (the server says so), so no closed message', (tester) async {
      await pumpDetails(tester, backendWith(openNow: true));
      await openMenu(tester);

      expect(find.text('Opening hours 06:00–20:00'), findsOneWidget);
      expect(find.textContaining('Hotline closed'), findsNothing);
    });

    testWidgets('openNow false: "Hotline closed – opens 06:00" (icon and words)', (tester) async {
      await pumpDetails(tester, backendWith(openNow: false));
      await openMenu(tester);

      expect(find.text('Hotline closed – opens 06:00'), findsOneWidget);
      expect(find.text('Opening hours 06:00–20:00'), findsOneWidget);
      expect(find.byIcon(Icons.schedule), findsOneWidget);
    });

    testWidgets('openNow comes from the server: no clock of the phone is looked at', (tester) async {
      // Even with a Date header at midday the closed answer wins.
      await pumpDetails(tester, backendWith(openNow: false)..cateringDateHeader = 'Tue, 20 Oct 2026 06:30:00 GMT');
      await openMenu(tester);

      expect(find.text('Hotline closed – opens 06:00'), findsOneWidget);
    });

    testWidgets('"Call to order" stays enabled when the hotline is closed', (tester) async {
      final launched = <Uri>[];
      await pumpDetails(tester, backendWith(openNow: false), launched: launched);
      await openMenu(tester);

      await tester.tap(find.text(Strings.callToOrder));
      await tester.pumpAndSettle();

      expect(launched, hasLength(1));
      expect(launched.single.path, '+94112345678');
    });

    testWidgets('no opening hours (null): the menu and the call button show, no hours line and no closed message, no crash', (tester) async {
      await pumpDetails(tester, backendWith(openNow: false, openingHours: null));
      await openMenu(tester);

      expect(find.text('Chicken rice'), findsOneWidget);
      expect(find.text(Strings.callToOrder), findsOneWidget);
      expect(find.textContaining('Opening hours'), findsNothing);
      expect(find.textContaining('Hotline closed'), findsNothing);
    });

    testWidgets('an empty hotline: the menu shows, but no call button and no "Mention" line', (tester) async {
      await pumpDetails(tester, backendWith(hotline: ''));
      await openMenu(tester);

      expect(find.text('Chicken rice'), findsOneWidget);
      expect(find.text(Strings.callToOrder), findsNothing);
      expect(find.textContaining('Mention:'), findsNothing);
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
      await pumpDetails(tester, backendWithFood()..catering = cateringJson(available: false, openNow: false, hotline: '', openingHours: null, menu: []));
      await openMenu(tester);

      expect(find.text(Strings.menuUnavailable), findsOneWidget);
      expect(find.text(Strings.callToOrder), findsNothing);
    });

    testWidgets('available:false shows "Menu unavailable" with Try again, and no call button', (tester) async {
      await pumpDetails(tester, backendWithFood()..catering = cateringJson(available: false, openNow: false, hotline: '', openingHours: null, menu: []));
      await openMenu(tester);

      expect(find.text(Strings.menuUnavailable), findsOneWidget);
      expect(find.text(Strings.tryAgain), findsOneWidget);
      expect(find.text(Strings.callToOrder), findsNothing);
    });

    testWidgets('"Menu unavailable" for an unknown station (404 NOT_FOUND)', (tester) async {
      await pumpDetails(tester, backendWithFood()..catering = null);
      await openMenu(tester);

      expect(find.text(Strings.menuUnavailable), findsOneWidget);
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
