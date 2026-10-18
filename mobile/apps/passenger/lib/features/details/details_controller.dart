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
  DetailController(this._api, this.journeyId, {Stopwatch Function()? newStopwatch})
      : _newStopwatch = newStopwatch ?? Stopwatch.new;

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
  final Stopwatch Function() _newStopwatch;

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
    if (_state is DetailLoaded) startLive();
  }

  /// Manual refresh (pull down or the refresh button). Keeps what is on screen
  /// while it runs; if it fails, the old data stays and is marked.
  Future<void> refresh() => _fetch();

  Future<void> _fetch() async {
    final id = ++_loadId;
    try {
      final response = await _api.getJourney(journeyId);
      if (id != _loadId) return;
      _ageAtReceipt = _ageFrom(response);
      _sinceReceipt = _newStopwatch()..start();
      _state = DetailLoaded(response.journey);
    } on ApiException {
      if (id != _loadId) return;
      final current = _state;
      _state = current is DetailLoaded ? DetailLoaded(current.journey, refreshFailed: true) : const DetailFailed();
    }
    notifyListeners();
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
        .openStream(journeyId, lastEventId: _lastEventId, silenceTimeout: silenceTimeout)
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
      case SnapshotMessage(:final sequence):
        // Load the journey again, so nothing between the first load and this
        // snapshot (or during an outage) is missed; older events are then ignored.
        _lastSequence = sequence;
        _scheduleRefetch();
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

  @override
  void dispose() {
    stopLive();
    super.dispose();
  }

  Duration? _ageFrom(JourneyResponse response) {
    final server = response.serverTime;
    final updated = response.journey.freshness.updatedAt;
    if (server == null || updated == null) return null;
    final age = server.difference(updated);
    return age.isNegative ? Duration.zero : age;
  }
}
