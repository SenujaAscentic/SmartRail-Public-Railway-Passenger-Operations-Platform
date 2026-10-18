import 'dart:async';
import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:smartrail_core/smartrail_core.dart';
import 'package:smartrail_passenger/features/details/details_controller.dart';

import '../../support/fake_backend.dart';
import 'details_screen_test.dart' show FakeStopwatch, thirtySecondsLater;

DetailController controllerFor(http.Client client, {FakeStopwatch? watch}) =>
    DetailController(SmartRailApi(client, 'http://test'), 'j', newStopwatch: () => watch ?? FakeStopwatch());

http.Response answer(Map<String, dynamic> detail, {String? date = thirtySecondsLater}) =>
    http.Response(jsonEncode(detail), 200, headers: {'date': ?date});

void main() {
  test('it starts loading, then shows the journey', () async {
    final c = controllerFor(MockClient((_) async => answer(detailJson())));
    expect(c.state, isA<DetailLoading>());

    await c.load();

    expect(c.state, isA<DetailLoaded>());
    expect((c.state as DetailLoaded).journey.serviceNumber, '1001');
    expect((c.state as DetailLoaded).refreshFailed, isFalse);
  });

  test('the age is the Date header minus updatedAt, rounded down, plus the stopwatch', () async {
    final watch = FakeStopwatch();
    final c = controllerFor(MockClient((_) async => answer(detailJson())), watch: watch);
    await c.load();

    expect(c.age!.inSeconds, 30);
    watch.passed = const Duration(minutes: 2);
    expect(c.age!.inSeconds, 150);
  });

  test('the age is unknown without a Date header, and without updatedAt', () async {
    final noDate = controllerFor(MockClient((_) async => answer(detailJson(), date: null)));
    await noDate.load();
    expect(noDate.age, isNull);

    final detail = detailJson();
    (detail['freshness'] as Map)['updatedAt'] = null;
    final noUpdate = controllerFor(MockClient((_) async => answer(detail)));
    await noUpdate.load();
    expect(noUpdate.age, isNull);
  });

  test('the age restarts at every successful load', () async {
    final watches = [FakeStopwatch()..passed = const Duration(minutes: 5), FakeStopwatch()];
    var n = 0;
    final c = DetailController(
      SmartRailApi(MockClient((_) async => answer(detailJson())), 'http://test'),
      'j',
      newStopwatch: () => watches[n++],
    );
    await c.load();
    expect(c.age!.inSeconds, 330);

    await c.refresh();
    expect(c.age!.inSeconds, 30);
  });

  test('a failed first load is DetailFailed, and load() can try again', () async {
    var fail = true;
    final c = controllerFor(MockClient((_) async => fail ? http.Response('boom', 500) : answer(detailJson())));

    await c.load();
    expect(c.state, isA<DetailFailed>());

    fail = false;
    await c.load();
    expect(c.state, isA<DetailLoaded>());
  });

  test('a failed refresh keeps the old journey and marks it', () async {
    var fail = false;
    final c = controllerFor(MockClient((_) async => fail ? http.Response('boom', 500) : answer(detailJson())));
    await c.load();

    fail = true;
    await c.refresh();

    final state = c.state as DetailLoaded;
    expect(state.refreshFailed, isTrue);
    expect(state.journey.serviceNumber, '1001');
    expect(c.age, isNotNull); // the old data keeps ageing

    fail = false;
    await c.refresh();
    expect((c.state as DetailLoaded).refreshFailed, isFalse);
  });

  test('an older, slower answer cannot replace a newer one', () async {
    final first = Completer<http.Response>();
    final second = Completer<http.Response>();
    final pending = [first, second];
    final c = controllerFor(MockClient((_) => pending.removeAt(0).future));

    final a = c.refresh();
    final b = c.refresh();
    final newer = detailJson()..['status'] = {'kind': 'Late', 'delayMinutes': 6};
    second.complete(answer(newer));
    await b;
    first.complete(answer(detailJson()));
    await a;

    expect((c.state as DetailLoaded).journey.status.kind, StatusKind.late);
  });
}
