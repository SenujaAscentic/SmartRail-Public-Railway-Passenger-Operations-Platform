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

/// Loads one journey and counts how old its data is (US-P2, US-P4 AC6).
/// The age is measured with the app's own stopwatch: at receipt it is the
/// server's `Date` header minus `updatedAt`, then it grows with the stopwatch
/// (ADR-12, point 12). Without a `Date` header no age is shown.
class DetailController extends ChangeNotifier {
  DetailController(this._api, this.journeyId, {Stopwatch Function()? newStopwatch})
      : _newStopwatch = newStopwatch ?? Stopwatch.new;

  final SmartRailApi _api;
  final String journeyId;
  final Stopwatch Function() _newStopwatch;

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

  Duration? _ageFrom(JourneyResponse response) {
    final server = response.serverTime;
    final updated = response.journey.freshness.updatedAt;
    if (server == null || updated == null) return null;
    final age = server.difference(updated);
    return age.isNegative ? Duration.zero : age;
  }
}
