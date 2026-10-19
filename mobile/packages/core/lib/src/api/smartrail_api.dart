import 'dart:async';
import 'dart:convert';
import 'dart:io' show HttpDate;

import 'package:http/http.dart' as http;

import '../models/journey_detail.dart';
import '../models/journey_summary.dart';
import '../models/station.dart';
import '../models/station_catering.dart';
import '../models/station_detail.dart';
import '../stream/sse_parser.dart';

/// A failed call: the server answered with an error, answered with something
/// that is not the expected JSON, or could not be reached at all.
class ApiException implements Exception {
  const ApiException(this.message, {this.statusCode, this.code});

  /// For logs and developers only; screens show their own wording (strings.dart).
  final String message;

  /// HTTP status; null when the server was not reached.
  final int? statusCode;

  /// The `code` of an RFC 9457 problem (API contract §6), e.g. `VALIDATION_FAILED`.
  final String? code;

  @override
  String toString() => 'ApiException($statusCode $code: $message)';
}

/// The live stream went quiet for longer than allowed (no bytes, not even the
/// 15-second heartbeat), so the connection is treated as lost.
class StreamLostException implements Exception {
  const StreamLostException();

  @override
  String toString() => 'StreamLostException()';
}

/// A journey and the server's time when it answered (the HTTP `Date` header),
/// which is the reference for the age of the data (ADR-12, point 12).
class JourneyResponse {
  const JourneyResponse(this.journey, this.serverTime);

  final JourneyDetail journey;

  /// Null when the answer had no usable `Date` header: then no age is shown.
  final DateTime? serverTime;
}

/// The catering of a station and the server's time when it answered.
class CateringResponse {
  const CateringResponse(this.catering, this.serverTime);

  final StationCatering catering;

  /// Null when the answer had no usable `Date` header.
  final DateTime? serverTime;
}

/// Public (no sign-in) endpoints of API contract §3.1 that the passenger app uses.
/// The [http.Client] is passed in, so tests can use a fake (ADR-12).
class SmartRailApi {
  SmartRailApi(this._client, String baseUrl) : _base = Uri.parse(baseUrl);

  final http.Client _client;
  final Uri _base;

  /// Type-ahead: up to 10 stations whose name matches [query].
  Future<List<Station>> searchStations(String query) async {
    final answer = await _get('/api/v1/stations', {'query': query});
    return _list(answer.body, Station.fromJson);
  }

  /// Direct journeys from station [fromId] to station [toId] on [date] (`YYYY-MM-DD`).
  /// [after] (a UTC time) and [limit] are for "next trains" after a cancelled
  /// train; without them the backend uses its defaults.
  Future<List<JourneySummary>> searchJourneys({
    required String fromId,
    required String toId,
    required String date,
    DateTime? after,
    int? limit,
  }) async {
    final answer = await _get('/api/v1/journeys/search', {
      'from': fromId,
      'to': toId,
      'date': date,
      if (after != null) 'after': after.toUtc().toIso8601String(),
      if (limit != null) 'limit': '$limit',
    });
    return _list(answer.body, JourneySummary.fromJson);
  }

  /// One station with its catering flag (`GET /stations/{id}`).
  Future<StationDetail> getStation(String stationId) async {
    final answer = await _get('/api/v1/stations/${Uri.encodeComponent(stationId)}', const {});
    return _object(answer.body, StationDetail.fromJson);
  }

  /// The catering of a station: the provider's cached menu, hotline and opening
  /// hours (`GET /stations/{id}/catering`, US-P8). [serverTime] (the `Date`
  /// header) is the reference for "is the hotline open now".
  Future<CateringResponse> getCatering(String stationId) async {
    final answer = await _get('/api/v1/stations/${Uri.encodeComponent(stationId)}/catering', const {});
    return CateringResponse(_object(answer.body, StationCatering.fromJson), answer.serverTime);
  }

  /// One journey with its stops, train set and last position.
  Future<JourneyResponse> getJourney(String journeyId) async {
    final answer = await _get(
      '/api/v1/journeys/${Uri.encodeComponent(journeyId)}',
      const {},
    );
    final body = answer.body;
    if (body is! Map<String, dynamic>) {
      throw const ApiException('Expected a JSON object');
    }
    try {
      return JourneyResponse(JourneyDetail.fromJson(body), answer.serverTime);
    } on FormatException catch (e) {
      throw ApiException('Unexpected data: ${e.message}');
    } on TypeError catch (e) {
      throw ApiException('Unexpected data: $e');
    }
  }

  /// The public live stream of one journey (`GET /api/v1/stream?journeyIds=`).
  /// Sends `Last-Event-ID` when resuming. The stream ends with an error
  /// ([ApiException] if the server refused, [StreamLostException] after
  /// [silenceTimeout] without any bytes) or normally when the server closes it.
  /// [onConnected] is called when the server has answered, with its time (the
  /// `Date` header; null if unusable): the reference for the age of the snapshot.
  /// Cancelling the subscription closes the connection at once.
  Stream<SseFrame> openStream(
    String journeyId, {
    String? lastEventId,
    Duration silenceTimeout = const Duration(seconds: 45),
    void Function(DateTime? serverTime)? onConnected,
  }) {
    late final StreamController<SseFrame> controller;
    StreamSubscription<SseFrame>? upstream;
    Timer? watchdog;
    var cancelled = false;

    Future<void> connect() async {
      final request = http.Request(
        'GET',
        _base.replace(path: '/api/v1/stream', queryParameters: {'journeyIds': journeyId}),
      )
        ..headers['Accept'] = 'text/event-stream'
        ..headers['Cache-Control'] = 'no-cache';
      if (lastEventId != null) request.headers['Last-Event-ID'] = lastEventId;

      try {
        final response = await _client.send(request);
        if (cancelled) {
          await response.stream.drain<void>();
          return;
        }
        if (response.statusCode != 200) {
          await response.stream.drain<void>();
          throw ApiException('HTTP ${response.statusCode}', statusCode: response.statusCode);
        }

        onConnected?.call(_serverTime(response));

        // Silence detection: every chunk of bytes (a frame or the heartbeat
        // comment) starts the wait again; if it runs out, the connection is
        // closed and the stream ends with a StreamLostException.
        void arm() {
          watchdog?.cancel();
          watchdog = Timer(silenceTimeout, () {
            upstream?.cancel();
            controller.addError(const StreamLostException());
            controller.close();
          });
        }

        arm();
        final bytes = response.stream.map((chunk) {
          arm();
          return chunk;
        });
        upstream = parseSse(bytes).listen(
          controller.add,
          onError: (Object error, StackTrace stack) {
            watchdog?.cancel();
            controller.addError(error, stack);
          },
          onDone: () {
            watchdog?.cancel();
            controller.close();
          },
        );
      } on http.ClientException catch (e) {
        controller.addError(ApiException('Server not reached: ${e.message}'));
        await controller.close();
      } on ApiException catch (e) {
        controller.addError(e);
        await controller.close();
      }
    }

    controller = StreamController<SseFrame>(
      onListen: connect,
      onCancel: () {
        cancelled = true;
        watchdog?.cancel();
        // Not returned: a controller that waits for this future before it
        // delivers "done" would hold the end of the stream back.
        unawaited(upstream?.cancel());
      },
    );
    return controller.stream;
  }

  Future<({Object? body, DateTime? serverTime})> _get(
    String path,
    Map<String, String> query,
  ) async {
    final uri = _base.replace(
      path: path,
      queryParameters: query.isEmpty ? null : query,
    );
    final http.Response response;
    try {
      response = await _client.get(
        uri,
        headers: {'Accept': 'application/json'},
      );
    } on http.ClientException catch (e) {
      throw ApiException('Server not reached: ${e.message}');
    }
    if (response.statusCode != 200) throw _errorFrom(response);
    try {
      return (
        body: jsonDecode(utf8.decode(response.bodyBytes)),
        serverTime: _serverTime(response),
      );
    } on FormatException catch (e) {
      throw ApiException(
        'Answer is not JSON: ${e.message}',
        statusCode: response.statusCode,
      );
    }
  }

  DateTime? _serverTime(http.BaseResponse response) {
    final header = response.headers['date'];
    if (header == null) return null;
    try {
      return HttpDate.parse(header);
    } on Exception {
      return null; // an unreadable Date header means "age unknown", not an error
    }
  }

  ApiException _errorFrom(http.Response response) {
    String? code;
    String message = 'HTTP ${response.statusCode}';
    try {
      final problem = jsonDecode(utf8.decode(response.bodyBytes));
      if (problem is Map<String, dynamic>) {
        code = problem['code'] as String?;
        message = (problem['title'] as String?) ?? message;
      }
    } on FormatException {
      // Not a problem document: the status code is all we know.
    }
    return ApiException(message, statusCode: response.statusCode, code: code);
  }

  T _object<T>(Object? body, T Function(Map<String, dynamic>) parse) {
    if (body is! Map<String, dynamic>) throw const ApiException('Expected a JSON object');
    try {
      return parse(body);
    } on FormatException catch (e) {
      throw ApiException('Unexpected data: ${e.message}');
    } on TypeError catch (e) {
      throw ApiException('Unexpected data: $e');
    }
  }

  List<T> _list<T>(Object? body, T Function(Map<String, dynamic>) parse) {
    if (body is! List) throw const ApiException('Expected a JSON list');
    try {
      return [for (final item in body) parse(item as Map<String, dynamic>)];
    } on FormatException catch (e) {
      throw ApiException('Unexpected data: ${e.message}');
    } on TypeError catch (e) {
      // A missing or wrongly typed field in an item.
      throw ApiException('Unexpected data: $e');
    }
  }
}
