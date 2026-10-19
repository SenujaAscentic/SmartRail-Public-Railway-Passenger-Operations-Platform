import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:smartrail_core/smartrail_core.dart';

// The two calls behind US-P8: the station's catering flag and the catering of a station.
void main() {
  const base = 'http://10.0.2.2:5045';
  const id = '01a14eb2-015b-7a7e-bd97-4eb8ac4d62df';

  test('getStation calls GET /api/v1/stations/{id} and reads the catering flag', () async {
    late Uri seen;
    final api = SmartRailApi(
      MockClient((request) async {
        seen = request.url;
        return http.Response(
          '{"stationId":"$id","code":"PLG","name":"Polgahawela","latitude":7.3,"longitude":80.3,"cateringStation":true,"platforms":[]}',
          200,
          headers: {'content-type': 'application/json'},
        );
      }),
      base,
    );

    final station = await api.getStation(id);

    expect(seen.toString(), '$base/api/v1/stations/$id');
    expect(station.name, 'Polgahawela');
    expect(station.cateringStation, isTrue);
  });

  test('getCatering calls GET /api/v1/stations/{id}/catering and returns the server time', () async {
    late Uri seen;
    final api = SmartRailApi(
      MockClient((request) async {
        seen = request.url;
        return http.Response(
          '{"available":true,"hotline":"+94112345678","openingHours":{"opens":"06:00","closes":"20:00"},"menu":[{"name":"Tea","price":80,"available":true}]}',
          200,
          headers: {'content-type': 'application/json', 'date': 'Tue, 20 Oct 2026 06:30:00 GMT'},
        );
      }),
      base,
    );

    final answer = await api.getCatering(id);

    expect(seen.toString(), '$base/api/v1/stations/$id/catering');
    expect(answer.catering.menu.single.name, 'Tea');
    expect(answer.serverTime, DateTime.utc(2026, 10, 20, 6, 30));
  });

  test('a catering answer without the required field is an ApiException, not a crash', () async {
    final api = SmartRailApi(MockClient((_) async => http.Response('{"hotline":"1"}', 200)), base);

    expect(() => api.getCatering(id), throwsA(isA<ApiException>()));
  });

  test('a failing provider (500) is an ApiException', () async {
    final api = SmartRailApi(MockClient((_) async => http.Response('boom', 500)), base);

    expect(() => api.getCatering(id), throwsA(isA<ApiException>()));
  });
}
