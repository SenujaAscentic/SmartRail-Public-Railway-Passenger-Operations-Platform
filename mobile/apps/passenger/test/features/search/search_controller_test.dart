import 'dart:async';
import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:smartrail_core/smartrail_core.dart';
import 'package:smartrail_passenger/features/search/search_controller.dart';

import '../../support/fake_backend.dart';

const gampaha = Station(id: 'g', code: 'GAM', name: 'Gampaha');
const kandy = Station(id: 'k', code: 'KDY', name: 'Kandy');

SearchController controllerFor(http.Client client) =>
    SearchController(SmartRailApi(client, 'http://test'), () => DateTime.utc(2026, 10, 13, 20, 0));

void main() {
  test('the date starts as today in Colombo, not in UTC', () {
    final c = controllerFor(MockClient((_) async => http.Response('[]', 200)));
    expect(c.date, '2026-10-14');
    expect(c.today, '2026-10-14');
  });

  test('search needs two different stations', () {
    final c = controllerFor(MockClient((_) async => http.Response('[]', 200)));
    expect(c.canSearch, isFalse);
    c.setFrom(gampaha);
    expect(c.canSearch, isFalse);
    c.setTo(gampaha);
    expect(c.canSearch, isFalse);
    c.setTo(kandy);
    expect(c.canSearch, isTrue);
  });

  test('search goes through loading to results, empty or failed', () async {
    var answer = http.Response('[]', 200);
    final c = controllerFor(MockClient((_) async => answer))
      ..setFrom(gampaha)
      ..setTo(kandy);
    final seen = <Type>[];
    c.addListener(() => seen.add(c.state.runtimeType));

    await c.search();
    expect(seen, [SearchLoading, SearchEmpty]);

    answer = http.Response(jsonEncode([journeyJson()]), 200);
    await c.search();
    expect(c.state, isA<SearchResults>());
    expect((c.state as SearchResults).journeys.single.serviceNumber, '1001');

    answer = http.Response('boom', 500);
    await c.search();
    expect(c.state, isA<SearchFailed>());
  });

  test('an older, slower answer cannot replace a newer one', () async {
    final first = Completer<http.Response>();
    final second = Completer<http.Response>();
    final pending = [first, second];
    final c = controllerFor(MockClient((_) => pending.removeAt(0).future))
      ..setFrom(gampaha)
      ..setTo(kandy);

    final a = c.search();
    final b = c.search();
    second.complete(http.Response(jsonEncode([journeyJson(serviceNumber: '2')]), 200));
    await b;
    first.complete(http.Response(jsonEncode([journeyJson(serviceNumber: '1')]), 200));
    await a;

    expect((c.state as SearchResults).journeys.single.serviceNumber, '2');
  });

  test('an answer that arrives after the form changed is dropped', () async {
    final answer = Completer<http.Response>();
    final c = controllerFor(MockClient((_) => answer.future))
      ..setFrom(gampaha)
      ..setTo(kandy);

    final search = c.search();
    c.setDate('2026-10-15');
    answer.complete(http.Response(jsonEncode([journeyJson()]), 200));
    await search;

    expect(c.state, isA<SearchIdle>());
  });

  test('suggestions fail quietly: no suggestions instead of an error', () async {
    final c = controllerFor(MockClient((_) async => http.Response('boom', 500)));
    expect(await c.suggestions('ka'), isEmpty);
  });
}
