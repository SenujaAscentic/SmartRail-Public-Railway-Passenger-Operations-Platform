import 'package:flutter/foundation.dart';
import 'package:smartrail_core/smartrail_core.dart';

/// What the menu screen shows (ADR-12: a sealed class and a switch).
sealed class CateringState {
  const CateringState();
}

class CateringLoading extends CateringState {
  const CateringLoading();
}

/// The provider's cached menu, with the time to judge the opening hours by.
class CateringLoaded extends CateringState {
  const CateringLoaded(this.catering, this.now);

  final StationCatering catering;

  /// The server's time when it answered (the `Date` header), else the phone's clock.
  final DateTime now;
}

/// "Menu unavailable": the provider is down and nothing is cached (the answer
/// says `available: false`, or has no menu), or the request failed.
class CateringUnavailable extends CateringState {
  const CateringUnavailable();
}

/// Loads the catering of one station (US-P8). The screen is the only place that
/// depends on it: a failure here never touches the train information.
class CateringController extends ChangeNotifier {
  CateringController(this._api, this.stationId, this._nowUtc);

  final SmartRailApi _api;
  final String stationId;
  final DateTime Function() _nowUtc;

  CateringState _state = const CateringLoading();
  CateringState get state => _state;
  bool _disposed = false;

  Future<void> load() async {
    _set(const CateringLoading());
    try {
      final answer = await _api.getCatering(stationId);
      final catering = answer.catering;
      _set(catering.available && catering.menu.isNotEmpty
          ? CateringLoaded(catering, answer.serverTime ?? _nowUtc())
          : const CateringUnavailable());
    } on ApiException {
      _set(const CateringUnavailable());
    }
  }

  void _set(CateringState state) {
    if (_disposed) return;
    _state = state;
    notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }
}
