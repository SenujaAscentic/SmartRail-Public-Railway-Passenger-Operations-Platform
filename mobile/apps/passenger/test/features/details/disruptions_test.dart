import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

import '../../support/fake_backend.dart';
import 'details_screen_test.dart' show backendWith, pumpDetails;

// Disruption states on the train details screen (US-P4 AC3 to AC5), against the
// contract shapes: a cancelled or terminated journey has Skipped stops, a hold
// and a reason come as objects, the other trains come from the journey search.

/// Marks the stops with these names as skipped (what cancel and terminate do).
void skip(Map<String, dynamic> detail, List<String> names) {
  for (final name in names) {
    stopNamed(detail, name)['status'] = 'Skipped';
  }
}

Map<String, dynamic> cancelled() {
  final detail = detailJson()..['status'] = {'kind': 'Cancelled', 'delayMinutes': null};
  skip(detail, ['Gampaha', 'Polgahawela', 'Peradeniya', 'Kandy']);
  return detail;
}

/// Ended early at Gampaha: everything after it is skipped.
Map<String, dynamic> terminatedAtGampaha() {
  final detail = detailJson()..['status'] = {'kind': 'TerminatedEarly', 'delayMinutes': null};
  skip(detail, ['Polgahawela', 'Peradeniya', 'Kandy']);
  return detail;
}

List<Uri> searches(FakeBackend b) => [for (final u in b.requests) if (u.path == '/api/v1/journeys/search') u];

FakeBackend withTrains(Map<String, dynamic> detail, [List<Map<String, dynamic>>? trains]) => backendWith(detail)
  ..journeys = trains ??
      [
        journeyJson(id: 'journey-1', serviceNumber: '1001', name: 'Kandy Express'),
        journeyJson(id: 'a', serviceNumber: '1003', name: 'Podi Menike', scheduledDeparture: '2026-10-20T03:00:00Z', expectedDeparture: '2026-10-20T03:00:00Z', scheduledArrival: '2026-10-20T05:00:00Z', expectedArrival: '2026-10-20T05:00:00Z'),
        journeyJson(id: 'b', serviceNumber: '1005', name: 'Udarata Menike', scheduledDeparture: '2026-10-20T04:00:00Z', expectedDeparture: '2026-10-20T04:00:00Z', scheduledArrival: '2026-10-20T06:00:00Z', expectedArrival: '2026-10-20T06:00:00Z'),
      ];

void main() {
  setUpAll(loadAppFonts);

  group('cancelled', () {
    testWidgets('shows "Cancelled" and the stops as not served', (tester) async {
      await pumpDetails(tester, withTrains(cancelled()), boarding: gampahaId, destination: kandyId);

      expect(find.text('Cancelled'), findsOneWidget);
      expect(find.text('Does not stop'), findsNWidgets(4));
    });

    testWidgets('asks the search for the next 3 trains: same stations, this train\'s departure, limit 3', (tester) async {
      final backend = withTrains(cancelled());
      await pumpDetails(tester, backend, boarding: gampahaId, destination: kandyId);

      final q = searches(backend).single.queryParameters;
      expect(q['from'], gampahaId);
      expect(q['to'], kandyId);
      expect(q['date'], '2026-10-20');
      // Gampaha: scheduled departure 01:42 UTC (the backend does the comparison, the app only passes it on).
      expect(q['after'], '2026-10-20T01:42:00.000Z');
      expect(q['limit'], '3');
    });

    testWidgets('lists the trains the backend sent, without this train itself', (tester) async {
      await pumpDetails(tester, withTrains(cancelled()), boarding: gampahaId, destination: kandyId);

      expect(find.text('Next trains'), findsOneWidget);
      expect(find.textContaining('1003 Podi Menike', findRichText: true), findsOneWidget);
      expect(find.textContaining('1005 Udarata Menike', findRichText: true), findsOneWidget);
      expect(find.textContaining('1001 Kandy Express', findRichText: true), findsNothing, reason: 'the cancelled train is not offered');
    });

    testWidgets('shows at most 3 trains, in the order sent', (tester) async {
      final trains = [for (var i = 0; i < 5; i++) journeyJson(id: 't$i', serviceNumber: '20$i', name: 'Train $i')];
      await pumpDetails(tester, withTrains(cancelled(), trains), boarding: gampahaId, destination: kandyId);

      expect(find.byType(JourneyCard), findsNWidgets(3));
      expect(find.textContaining('200 Train 0', findRichText: true), findsOneWidget);
      expect(find.textContaining('202 Train 2', findRichText: true), findsOneWidget);
      expect(find.textContaining('203 Train 3', findRichText: true), findsNothing);
    });

    testWidgets('a tap on an alternative opens it', (tester) async {
      final opened = <JourneySummary>[];
      await pumpDetails(tester, withTrains(cancelled()), boarding: gampahaId, destination: kandyId, opened: opened);

      await tester.tap(find.textContaining('1003 Podi Menike', findRichText: true));

      expect(opened.single.serviceNumber, '1003');
      expect(opened.single.journeyId, 'a');
    });

    testWidgets('no other train: says so', (tester) async {
      await pumpDetails(tester, withTrains(cancelled(), []), boarding: gampahaId, destination: kandyId);

      expect(find.text('No other direct trains on this date'), findsOneWidget);
    });

    testWidgets('a failing search shows an error with "Try again"', (tester) async {
      final backend = withTrains(cancelled())..failJourneySearch = true;
      await pumpDetails(tester, backend, boarding: gampahaId, destination: kandyId);
      expect(find.text("Couldn't load other trains"), findsOneWidget);

      backend.failJourneySearch = false;
      await tester.tap(find.text('Try again'));
      await tester.pumpAndSettle();

      expect(find.text("Couldn't load other trains"), findsNothing);
      expect(find.text('Next trains'), findsOneWidget);
    });

    testWidgets('without the passenger\'s stations it uses the journey\'s own ends', (tester) async {
      final backend = withTrains(cancelled());
      await pumpDetails(tester, backend);

      final q = searches(backend).single.queryParameters;
      expect(q['from'], detailJson()['from']['stationId']);
      expect(q['to'], kandyId);
    });
  });

  group('terminated early', () {
    testWidgets('says where it ends: "Terminates at Gampaha"', (tester) async {
      await pumpDetails(tester, withTrains(terminatedAtGampaha()), boarding: gampahaId, destination: kandyId);

      expect(find.text('Terminates at Gampaha'), findsOneWidget);
      expect(find.text('Does not stop'), findsNWidgets(3));
    });

    testWidgets('offers other trains from where it ends to the destination, with no "after"', (tester) async {
      final backend = withTrains(terminatedAtGampaha());
      await pumpDetails(tester, backend, boarding: gampahaId, destination: kandyId);

      final q = searches(backend).single.queryParameters;
      expect(q['from'], gampahaId, reason: 'where this train ends');
      expect(q['to'], kandyId);
      expect(q.containsKey('after'), isFalse);
      expect(q['limit'], '3');
      expect(find.text('Other trains'), findsOneWidget);
    });

    testWidgets('a passenger whose destination is still served gets no alternatives', (tester) async {
      final backend = withTrains(terminatedAtGampaha());
      await pumpDetails(tester, backend, boarding: stopNamed(detailJson(), 'Maradana')['stationId'] as String, destination: gampahaId);

      expect(searches(backend), isEmpty);
      expect(find.text('Other trains'), findsNothing);
      expect(find.text('Terminates at Gampaha'), findsOneWidget, reason: 'the status is still shown');
    });

    testWidgets('a destination before the end of the line is still served: no alternatives', (tester) async {
      final detail = detailJson()..['status'] = {'kind': 'TerminatedEarly', 'delayMinutes': null};
      skip(detail, ['Kandy']); // ends at Peradeniya
      final backend = withTrains(detail);
      await pumpDetails(tester, backend, boarding: gampahaId, destination: stopNamed(detail, 'Polgahawela')['stationId'] as String);

      expect(searches(backend), isEmpty);
      expect(find.text('Other trains'), findsNothing);
    });

    testWidgets('a passenger whose own station is no longer served gets the next trains from there', (tester) async {
      final detail = detailJson()..['status'] = {'kind': 'TerminatedEarly', 'delayMinutes': null};
      skip(detail, ['Gampaha', 'Polgahawela', 'Peradeniya', 'Kandy']); // ends at Maradana
      final backend = withTrains(detail);
      await pumpDetails(tester, backend, boarding: gampahaId, destination: kandyId);

      final q = searches(backend).single.queryParameters;
      expect(q['from'], gampahaId);
      expect(q['after'], '2026-10-20T01:42:00.000Z');
      expect(find.text('Next trains'), findsOneWidget);
      expect(find.text('Terminates at Maradana'), findsOneWidget);
    });

    testWidgets('without a destination: alternatives from where it ends to the journey\'s end', (tester) async {
      final backend = withTrains(terminatedAtGampaha());
      await pumpDetails(tester, backend);

      final q = searches(backend).single.queryParameters;
      expect(q['from'], gampahaId);
      expect(q['to'], kandyId);
    });
  });

  group('a train that runs normally', () {
    testWidgets('does not search for alternatives', (tester) async {
      final backend = withTrains(detailJson());
      await pumpDetails(tester, backend, boarding: gampahaId, destination: kandyId);

      expect(searches(backend), isEmpty);
      expect(find.text('Next trains'), findsNothing);
      expect(find.text('Other trains'), findsNothing);
    });
  });

  group('platform change', () {
    testWidgets('"Platform 5 (was 2)" in the header and on the stop row', (tester) async {
      final detail = detailJson();
      final gampaha = stopNamed(detail, 'Gampaha');
      gampaha['platform'] = '5';
      gampaha['previousPlatform'] = '2';
      await pumpDetails(tester, backendWith(detail), boarding: gampahaId, destination: kandyId);

      expect(find.text('Platform 5 (was 2)'), findsNWidgets(2));
      expect(find.byIcon(Icons.swap_horiz), findsNWidgets(2), reason: 'icon + text + colour in both places');
    });

    testWidgets('a platform change at another station shows on its row only', (tester) async {
      final detail = detailJson();
      final polgahawela = stopNamed(detail, 'Polgahawela');
      polgahawela['platform'] = '5';
      polgahawela['previousPlatform'] = '2';
      await pumpDetails(tester, backendWith(detail), boarding: gampahaId, destination: kandyId);

      expect(find.text('Platform 5 (was 2)'), findsOneWidget);
    });
  });

  group('held', () {
    Map<String, dynamic> held({Map<String, dynamic>? reason}) => detailJson()
      ..['status'] = {'kind': 'Held', 'delayMinutes': 3}
      ..['hold'] = {'stationId': gampahaId, 'minutes': 10, 'reason': reason};

    testWidgets('"Held · 3 min late" (S-08)', (tester) async {
      await pumpDetails(tester, backendWith(held()));

      expect(find.text('Held · 3 min late'), findsOneWidget);
      expect(find.byIcon(Icons.pan_tool), findsOneWidget);
    });

    testWidgets('shows the hold reason when sent', (tester) async {
      await pumpDetails(tester, backendWith(held(reason: {'code': 'Congestion', 'note': 'Waiting for the platform'})));

      expect(find.text('Reason: Congestion · Waiting for the platform'), findsOneWidget);
    });

    testWidgets('no reason sent: no reason line, and no promise that the train will wait', (tester) async {
      await pumpDetails(tester, backendWith(held()));

      expect(find.textContaining('Reason'), findsNothing);
      expect(find.textContaining('will wait'), findsNothing);
    });

    testWidgets('no hold: no hold text at all', (tester) async {
      await pumpDetails(tester, backendWith(detailJson()));

      expect(find.textContaining('Held'), findsNothing);
      expect(find.textContaining('Reason'), findsNothing);
    });
  });

  group('reason of a delay', () {
    testWidgets('shown when given: label and note', (tester) async {
      final detail = detailJson()
        ..['status'] = {'kind': 'Late', 'delayMinutes': 8}
        ..['reason'] = {'code': 'SignalFailure', 'note': 'signal fault near Ragama'};
      await pumpDetails(tester, backendWith(detail));

      expect(find.text('8 min late'), findsOneWidget);
      expect(find.text('Reason: Signal failure · signal fault near Ragama'), findsOneWidget);
    });

    testWidgets('code only', (tester) async {
      final detail = detailJson()..['reason'] = {'code': 'Weather', 'note': null};
      await pumpDetails(tester, backendWith(detail));

      expect(find.text('Reason: Weather'), findsOneWidget);
    });

    testWidgets('the note is plain text, never markup', (tester) async {
      final detail = detailJson()..['reason'] = {'code': 'Other', 'note': '<b>Bridge</b> check & <i>more</i>'};
      await pumpDetails(tester, backendWith(detail));

      expect(find.text('Reason: <b>Bridge</b> check & <i>more</i>'), findsOneWidget);
    });

    testWidgets('a cancelled train shows its reason too', (tester) async {
      final detail = cancelled()..['reason'] = {'code': 'TrainFault', 'note': 'Engine failure'};
      await pumpDetails(tester, withTrains(detail), boarding: gampahaId, destination: kandyId);

      expect(find.text('Reason: Train fault · Engine failure'), findsOneWidget);
    });

    testWidgets('a hold reason and a delay reason are both shown', (tester) async {
      final detail = detailJson()
        ..['status'] = {'kind': 'Held', 'delayMinutes': 5}
        ..['reason'] = {'code': 'TrackWork', 'note': null}
        ..['hold'] = {
          'stationId': gampahaId,
          'minutes': 5,
          'reason': {'code': 'Congestion', 'note': null},
        };
      await pumpDetails(tester, backendWith(detail));

      expect(find.text('Reason: Track work'), findsOneWidget);
      expect(find.text('Reason: Congestion'), findsOneWidget);
    });
  });

  group('stops that are skipped or added', () {
    testWidgets('"Does not stop" and "Extra stop" on the list', (tester) async {
      final detail = detailJson();
      stopNamed(detail, 'Polgahawela')['status'] = 'Skipped';
      stopNamed(detail, 'Peradeniya')['isAdded'] = true;
      await pumpDetails(tester, backendWith(detail));

      expect(find.text('Does not stop'), findsOneWidget);
      expect(find.text('Extra stop'), findsOneWidget);
    });
  });
}
