import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:smartrail_core/smartrail_core.dart';

String fixture(String name) => File('test/fixtures/$name').readAsStringSync();

void main() {
  const base = 'http://10.0.2.2:5045';

  test('searchStations calls GET /api/v1/stations?query= and parses the list', () async {
    late Uri seen;
    final api = SmartRailApi(
      MockClient((request) async {
        seen = request.url;
        return http.Response(fixture('stations.json'), 200, headers: {'content-type': 'application/json'});
      }),
      base,
    );

    final stations = await api.searchStations('ka');

    expect(seen.toString(), '$base/api/v1/stations?query=ka');
    expect(stations.map((s) => s.name), ['Gampaha', 'Kandy']);
  });

  test('searchJourneys sends from, to and date and parses the summaries', () async {
    late Uri seen;
    final api = SmartRailApi(
      MockClient((request) async {
        seen = request.url;
        return http.Response('[${fixture('journey_summary.json')}]', 200);
      }),
      base,
    );

    final journeys = await api.searchJourneys(fromId: 'a', toId: 'b', date: '2026-10-14');

    expect(seen.path, '/api/v1/journeys/search');
    expect(seen.queryParameters, {'from': 'a', 'to': 'b', 'date': '2026-10-14'});
    expect(journeys.single.serviceNumber, '1001');
  });

  test('an empty list is a normal answer', () async {
    final api = SmartRailApi(MockClient((_) async => http.Response('[]', 200)), base);
    expect(await api.searchJourneys(fromId: 'a', toId: 'b', date: '2026-10-14'), isEmpty);
  });

  test('a problem answer becomes an ApiException with the code', () async {
    final api = SmartRailApi(
      MockClient((_) async => http.Response(
            jsonEncode({'title': 'Invalid', 'status': 400, 'code': 'VALIDATION_FAILED'}),
            400,
            headers: {'content-type': 'application/problem+json'},
          )),
      base,
    );
    await expectLater(
      api.searchStations('x'),
      throwsA(isA<ApiException>().having((e) => e.code, 'code', 'VALIDATION_FAILED').having((e) => e.statusCode, 'status', 400)),
    );
  });

  test('a 500 without a problem body is an ApiException with the status', () async {
    final api = SmartRailApi(MockClient((_) async => http.Response('oops', 500)), base);
    await expectLater(api.searchStations('x'), throwsA(isA<ApiException>().having((e) => e.statusCode, 'status', 500)));
  });

  test('an unreachable server is an ApiException without a status', () async {
    final api = SmartRailApi(MockClient((_) async => throw http.ClientException('no route')), base);
    await expectLater(api.searchStations('x'), throwsA(isA<ApiException>().having((e) => e.statusCode, 'status', isNull)));
  });

  test('an item with a missing field is an ApiException, not a crash', () async {
    final api = SmartRailApi(MockClient((_) async => http.Response('[{"id":"1","code":"X"}]', 200)), base);
    await expectLater(api.searchStations('x'), throwsA(isA<ApiException>()));
  });

  test('an answer that is not a list is an ApiException', () async {
    final api = SmartRailApi(MockClient((_) async => http.Response('{}', 200)), base);
    await expectLater(api.searchStations('x'), throwsA(isA<ApiException>()));
  });

  group('getJourney', () {
    test('calls GET /api/v1/journeys/{id} and returns the journey with the server time', () async {
      late Uri seen;
      final api = SmartRailApi(
        MockClient((request) async {
          seen = request.url;
          return http.Response(fixture('journey_detail.json'), 200, headers: {'date': 'Tue, 20 Oct 2026 01:13:38 GMT'});
        }),
        base,
      );

      final response = await api.getJourney('01a156ce-00e7-729d-a368-ef00172f4ec5');

      expect(seen.toString(), '$base/api/v1/journeys/01a156ce-00e7-729d-a368-ef00172f4ec5');
      expect(response.journey.serviceNumber, '1001');
      expect(response.serverTime, DateTime.utc(2026, 10, 20, 1, 13, 38));
    });

    test('without a Date header the server time is null (no age is shown)', () async {
      final api = SmartRailApi(MockClient((_) async => http.Response(fixture('journey_detail.json'), 200)), base);
      expect((await api.getJourney('x')).serverTime, isNull);
    });

    test('an unreadable Date header is also "unknown", not an error', () async {
      final api = SmartRailApi(
        MockClient((_) async => http.Response(fixture('journey_detail.json'), 200, headers: {'date': 'yesterday-ish'})),
        base,
      );
      expect((await api.getJourney('x')).serverTime, isNull);
    });

    test('an unknown journey is an ApiException with status 404', () async {
      final api = SmartRailApi(MockClient((_) async => http.Response('{"code":"NOT_FOUND","title":"Not found"}', 404)), base);
      await expectLater(
        api.getJourney('x'),
        throwsA(isA<ApiException>().having((e) => e.statusCode, 'status', 404).having((e) => e.code, 'code', 'NOT_FOUND')),
      );
    });

    test('an answer with a missing field is an ApiException, not a crash', () async {
      final api = SmartRailApi(MockClient((_) async => http.Response('{"journeyId":"x"}', 200)), base);
      await expectLater(api.getJourney('x'), throwsA(isA<ApiException>()));
    });
  });
}
