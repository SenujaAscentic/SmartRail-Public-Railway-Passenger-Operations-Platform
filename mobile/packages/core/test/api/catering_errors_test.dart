import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:smartrail_core/smartrail_core.dart';

// What the catering call does with the answers that mean "Menu unavailable" (US-P8).
void main() {
  const id = '01a14eb2-015b-7a7e-bd97-4eb8ac4d62df';

  test('404 NOT_FOUND (unknown station) is an ApiException with the code', () async {
    final api = SmartRailApi(
      MockClient((_) async => http.Response('{"code":"NOT_FOUND","title":"Not found"}', 404)),
      'http://test',
    );

    await expectLater(
      api.getCatering(id),
      throwsA(isA<ApiException>().having((e) => e.code, 'code', 'NOT_FOUND').having((e) => e.statusCode, 'status', 404)),
    );
  });

  test('an answer without openNow is an ApiException, not a crash', () async {
    final api = SmartRailApi(
      MockClient((_) async => http.Response('{"available":true,"hotline":"1","openingHours":null,"menu":[]}', 200)),
      'http://test',
    );

    await expectLater(api.getCatering(id), throwsA(isA<ApiException>()));
  });

  test('the not-available answer (menu [], hotline "", hours null) parses', () async {
    final api = SmartRailApi(
      MockClient((_) async => http.Response('{"available":false,"openNow":false,"hotline":"","openingHours":null,"menu":[]}', 200)),
      'http://test',
    );

    final answer = await api.getCatering(id);

    expect(answer.catering.available, isFalse);
    expect(answer.catering.hasHotline, isFalse);
  });
}
