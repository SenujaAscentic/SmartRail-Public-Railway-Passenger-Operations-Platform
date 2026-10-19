import 'package:flutter/foundation.dart';
import 'package:smartrail_core/smartrail_core.dart';

/// What the menu screen shows (ADR-12: a sealed class and a switch).
sealed class CateringState {
  const CateringState();
}

class CateringLoading extends CateringState {
  const CateringLoading();
}

/// The provider's cached menu, hotline and opening hours (whether the hotline is open now is the server's `openNow`).
class CateringLoaded extends CateringState {
  const CateringLoaded(this.catering);

  final StationCatering catering;
}

/// "Menu unavailable": the answer says `available: false` (provider down, cache older than 15 minutes, not taking orders), or it has
/// no menu, or the request failed (404 for an unknown station too).
class CateringUnavailable extends CateringState {
  const CateringUnavailable();
}

/// Loads the catering of one station (US-P8). The screen is the only place that
/// depends on it: a failure here never touches the train information.
class CateringController extends ChangeNotifier {
  CateringController(this._api, this.stationId);

  final SmartRailApi _api;
  final String stationId;

  CateringState _state = const CateringLoading();
  CateringState get state => _state;
  bool _disposed = false;

  Future<void> load() async {
    _set(const CateringLoading());
    try {
      final answer = await _api.getCatering(stationId);
      final catering = answer.catering;
      _set(catering.available && catering.menu.isNotEmpty
          ? CateringLoaded(catering)
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
