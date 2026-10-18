import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:smartrail_core/smartrail_core.dart';

/// What the train details screen shows (ADR-12: a sealed class and a switch).
sealed class DetailState {
  const DetailState();
}

class DetailLoading extends DetailState {
  const DetailLoading();
}

/// The journey is shown. [refreshFailed] means the last refresh did not work, so
/// what is on screen is older than it looks (the screen says so).
class DetailLoaded extends DetailState {
  const DetailLoaded(this.journey, {this.refreshFailed = false});

  final JourneyDetail journey;
  final bool refreshFailed;
}

/// The first load failed: nothing to show yet.
class DetailFailed extends DetailState {
  const DetailFailed();
}

/// The trains offered when this one is cancelled or ends early.
sealed class AlternativesState {
  const AlternativesState();
}

/// Nothing to offer: the train runs for this passenger.
class NoAlternatives extends AlternativesState {
  const NoAlternatives();
}

class AlternativesLoading extends AlternativesState {
  const AlternativesLoading();
}

/// [trains] come from the journey search, as the backend sent them (at most 3).
/// [kind] decides the heading: the next trains, or other trains from where this one ends.
class AlternativesLoaded extends AlternativesState {
  const AlternativesLoaded(this.trains, this.kind);

  final List<JourneySummary> trains;
  final AlternativesKind kind;
}

class AlternativesFailed extends AlternativesState {
  const AlternativesFailed();
}

enum AlternativesKind { next, other }

/// The search that finds the alternatives (same shape as the search of US-P1).
class _AlternativesQuery {
  const _AlternativesQuery(this.kind, this.fromId, this.toId, this.date, this.after);

  final AlternativesKind kind;
  final String fromId;
  final String toId;
  final String date;
  final DateTime? after;

  @override
  bool operator ==(Object other) =>
      other is _AlternativesQuery &&
      other.kind == kind &&
      other.fromId == fromId &&
      other.toId == toId &&
      other.date == date &&
      other.after == after;

  @override
  int get hashCode => Object.hash(kind, fromId, toId, date, after);
}

/// Loads one journey, keeps it up to date from the live stream, and counts how
/// old its data is (US-P2, US-P3, US-P4).
///
/// Age: measured with the app's own stopwatch. At receipt it is the server's
/// `Date` header minus `updatedAt`, then it grows with the stopwatch (ADR-12,
/// point 12). Without a `Date` header no age is shown. A new position from the
/// stream resets it to zero.
///
/// Live updates: while the screen is open, the public stream of this journey is
/// read. A train position is applied directly; every other event makes the
/// journey load again (debounced), because the app never works out business
/// changes itself. If the stream goes quiet or breaks, it reconnects with a
/// growing pause (resuming with Last-Event-ID) and meanwhile polls the journey
/// every 30 seconds (ADR-02 fallback).
class DetailController extends ChangeNotifier {
  DetailController(
    this._api,
    this.journeyId, {
    this.boardingStationId,
    this.destinationStationId,
    Stopwatch Function()? newStopwatch,
  }) : _newStopwatch = newStopwatch ?? Stopwatch.new;

  /// The heartbeat comes every 15 s; three missed ones mean the connection is lost.
  static const silenceTimeout = Duration(seconds: 45);

  /// While the stream is down, the journey is loaded this often.
  static const pollInterval = Duration(seconds: 30);

  /// Several events in a row cause one reload.
  static const refetchDelay = Duration(milliseconds: 500);

  /// Pause before reconnecting: 1, 2, 4, 8, 16 s, then every 30 s.
  static const reconnectDelays = [
    Duration(seconds: 1),
    Duration(seconds: 2),
    Duration(seconds: 4),
    Duration(seconds: 8),
    Duration(seconds: 16),
    Duration(seconds: 30),
  ];

  final SmartRailApi _api;
  final String journeyId;

  /// The passenger's stations from the search: they mark "Your station" and
  /// "destination", and decide which alternatives make sense. Null when unknown.
  final String? boardingStationId;
  final String? destinationStationId;
  final Stopwatch Function() _newStopwatch;

  // Alternatives (cancelled / terminated early)
  AlternativesState _alternatives = const NoAlternatives();
  _AlternativesQuery? _alternativesQuery;
  int _alternativesId = 0;
  AlternativesState get alternatives => _alternatives;

  /// Server time of the stream answer: the reference for the age of a snapshot.
  DateTime? _streamServerTime;

  // Live updates
  bool _live = false;
  StreamSubscription<SseFrame>? _subscription;
  Timer? _reconnectTimer;
  Timer? _pollTimer;
  Timer? _refetchTimer;
  int _failures = 0;
  String? _lastEventId;
  int? _lastSequence;

  /// True while the stream is connected (the first frame has arrived).
  bool get connected => _connected;
  bool _connected = false;

  DetailState _state = const DetailLoading();
  Duration? _ageAtReceipt;
  Stopwatch? _sinceReceipt;

  /// Counts loads, so that only the newest answer is used.
  int _loadId = 0;

  DetailState get state => _state;

  /// How old the journey's data is now; null when unknown.
  Duration? get age {
    final base = _ageAtReceipt;
    final watch = _sinceReceipt;
    if (base == null || watch == null) return null;
    return base + watch.elapsed;
  }

  /// First load, or "Try again" after a failed first load.
  Future<void> load() async {
    _state = const DetailLoading();
    notifyListeners();
    await _fetch();
    final state = _state;
    if (state is DetailLoaded && state.journey.status.kind != StatusKind.completed) startLive();
  }

  /// Manual refresh (pull down or the refresh button). Keeps what is on screen
  /// while it runs; if it fails, the old data stays and is marked.
  Future<void> refresh() => _fetch();

  Future<void> _fetch() async {
    final id = ++_loadId;
    try {
      final response = await _api.getJourney(journeyId);
      if (id != _loadId) return;
      _show(response.journey, response.serverTime);
    } on ApiException {
      if (id != _loadId) return;
      final current = _state;
      _state = current is DetailLoaded ? DetailLoaded(current.journey, refreshFailed: true) : const DetailFailed();
    }
    notifyListeners();
  }

  /// Shows [journey] (from a load or a snapshot) and starts its age.
  /// A completed journey has nothing more to report, so the stream and the
  /// polling stop. A cancelled one keeps listening: it can be reinstated.
  void _show(JourneyDetail journey, DateTime? serverTime) {
    _ageAtReceipt = _ageFor(journey, serverTime);
    _sinceReceipt = _newStopwatch()..start();
    _state = DetailLoaded(journey);
    if (journey.status.kind == StatusKind.completed) stopLive();
    _updateAlternatives(journey);
  }

  /// Starts reading the stream. Called after the first successful load.
  void startLive() {
    if (_live) return;
    _live = true;
    _connect();
  }

  /// Stops the stream and every timer. Called when the screen closes.
  void stopLive() {
    _live = false;
    _connected = false;
    _subscription?.cancel();
    _subscription = null;
    _reconnectTimer?.cancel();
    _pollTimer?.cancel();
    _refetchTimer?.cancel();
  }

  void _connect() {
    if (!_live) return;
    _subscription?.cancel();
    _subscription = _api
        .openStream(
          journeyId,
          lastEventId: _lastEventId,
          silenceTimeout: silenceTimeout,
          onConnected: (serverTime) => _streamServerTime = serverTime,
        )
        .listen(_onFrame, onError: (_) => _onLost(), onDone: _onLost, cancelOnError: true);
  }

  void _onFrame(SseFrame frame) {
    if (!_connected) {
      // The first frame (the snapshot) proves the connection works.
      _connected = true;
      _failures = 0;
      _pollTimer?.cancel();
    }
    if (frame.id != null) _lastEventId = frame.id;

    switch (parseLiveFrame(frame)) {
      case SnapshotMessage(:final sequence, :final journey):
        // The snapshot is the state at this sequence (the public journey
        // detail): it replaces what is shown, so nothing between the first load
        // and now (or during an outage) is missed; older events are then
        // ignored. Without a readable journey in it, load the journey instead.
        _lastSequence = sequence;
        if (journey != null && journey.journeyId == journeyId) {
          _refetchTimer?.cancel();
          _show(journey, _streamServerTime);
          notifyListeners();
        } else {
          _scheduleRefetch();
        }
      case ResyncMessage():
        _lastEventId = null;
        _lastSequence = null;
        _refetchTimer?.cancel();
        refresh();
      case PositionMessage m:
        if (_isOld(m.sequence) || m.journeyId != journeyId) return;
        _lastSequence = m.sequence;
        _applyPosition(m);
      case ChangeMessage m:
        if (m.journeyId != null && m.journeyId != journeyId) return;
        final sequence = m.sequence;
        if (sequence != null) {
          if (_isOld(sequence)) return;
          _lastSequence = sequence;
        }
        _scheduleRefetch();
    }
  }

  /// An event with a sequence not above the last one seen is already reflected.
  bool _isOld(int sequence) => _lastSequence != null && sequence <= _lastSequence!;

  void _applyPosition(PositionMessage m) {
    final current = _state;
    if (current is! DetailLoaded) return;
    _state = DetailLoaded(current.journey.withPosition(m.position, m.occurredAt), refreshFailed: current.refreshFailed);
    // The position is new data: its age starts again at zero.
    _ageAtReceipt = Duration.zero;
    _sinceReceipt = _newStopwatch()..start();
    notifyListeners();
  }

  void _scheduleRefetch() {
    _refetchTimer?.cancel();
    _refetchTimer = Timer(refetchDelay, refresh);
  }

  void _onLost() {
    if (!_live) return;
    _connected = false;
    _subscription = null;
    // While the stream is down, load the journey now and then, so the screen does not go silent.
    // (Started once: later failed reconnects must not push the next poll back.)
    if (!(_pollTimer?.isActive ?? false)) {
      _pollTimer = Timer.periodic(pollInterval, (_) => refresh());
    }
    final delay = reconnectDelays[_failures < reconnectDelays.length ? _failures : reconnectDelays.length - 1];
    _failures++;
    _reconnectTimer?.cancel();
    _reconnectTimer = Timer(delay, _connect);
  }

  /// "Try again" for a failed list of alternatives.
  void retryAlternatives() {
    final current = _state;
    if (current is! DetailLoaded) return;
    _alternativesQuery = null; // forget it, so that the same query is run again
    _updateAlternatives(current.journey);
    notifyListeners();
  }

  /// Cancelled: the next trains from the passenger station to their
  /// destination, after this train departure. Ended early: other trains from
  /// where it ends, but only when the passenger destination is no longer
  /// served (or their own station is not). The backend does the search and the
  /// times; the app only asks and shows.
  _AlternativesQuery? _queryFor(JourneyDetail journey) {
    final fromId = boardingStationId ?? journey.from.stationId;
    final toId = destinationStationId ?? journey.to.stationId;
    final boarding = journey.stopAt(fromId);
    final destination = journey.stopAt(toId);
    final departure = boarding?.scheduledDeparture ?? boarding?.scheduledArrival ?? journey.from.scheduledDeparture;

    switch (journey.status.kind) {
      case StatusKind.cancelled:
        return _AlternativesQuery(AlternativesKind.next, fromId, toId, journey.serviceDate, departure);
      case StatusKind.terminatedEarly:
        if (boarding != null && boarding.state == StopState.skipped) {
          return _AlternativesQuery(AlternativesKind.next, fromId, toId, journey.serviceDate, departure);
        }
        final end = journey.terminatesAt;
        final destinationLost = destination == null || destination.state == StopState.skipped;
        if (destinationLost && end != null && end.stationId != toId) {
          return _AlternativesQuery(AlternativesKind.other, end.stationId, toId, journey.serviceDate, null);
        }
        return null;
      default:
        return null;
    }
  }

  void _updateAlternatives(JourneyDetail journey) {
    final query = _queryFor(journey);
    if (query == null) {
      _alternativesQuery = null;
      _alternativesId++;
      _alternatives = const NoAlternatives();
      return;
    }
    if (query == _alternativesQuery) return; // same situation: keep what is there
    _alternativesQuery = query;
    final id = ++_alternativesId;
    _alternatives = const AlternativesLoading();
    unawaited(_loadAlternatives(query, id));
  }

  Future<void> _loadAlternatives(_AlternativesQuery query, int id) async {
    AlternativesState result;
    try {
      final found = await _api.searchJourneys(
        fromId: query.fromId,
        toId: query.toId,
        date: query.date,
        after: query.after,
        limit: 3,
      );
      // This train itself is not an alternative to itself.
      result = AlternativesLoaded([for (final j in found) if (j.journeyId != journeyId) j].take(3).toList(), query.kind);
    } on ApiException {
      result = const AlternativesFailed();
    }
    if (id != _alternativesId) return; // the situation changed meanwhile
    _alternatives = result;
    notifyListeners();
  }

  @override
  void dispose() {
    stopLive();
    super.dispose();
  }

  Duration? _ageFor(JourneyDetail journey, DateTime? server) {
    final updated = journey.freshness.updatedAt;
    if (server == null || updated == null) return null;
    final age = server.difference(updated);
    return age.isNegative ? Duration.zero : age;
  }
}
