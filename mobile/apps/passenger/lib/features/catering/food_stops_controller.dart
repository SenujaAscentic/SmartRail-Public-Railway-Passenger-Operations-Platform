import 'package:flutter/foundation.dart';
import 'package:smartrail_core/smartrail_core.dart';

/// Which calling points of a journey have catering (US-P8 AC1). The journey does
/// not say; each station does (`GET /stations/{id}`, field `cateringStation`).
/// The answer for a station is kept, because the flag does not change while the
/// screen is open. A station that cannot be read is simply left out (train
/// information never depends on the provider) and tried again on the next update.
class FoodStopsController extends ChangeNotifier {
  FoodStopsController(this._api);

  final SmartRailApi _api;
  final Map<String, bool> _hasFood = {};
  final Set<String> _asking = {};
  bool _disposed = false;

  bool hasFood(String stationId) => _hasFood[stationId] ?? false;

  /// The stations to check now: those not answered yet are asked, all at once.
  void update(Iterable<String> stationIds) {
    for (final id in stationIds) {
      if (_hasFood.containsKey(id) || !_asking.add(id)) continue;
      _ask(id);
    }
  }

  Future<void> _ask(String id) async {
    try {
      final station = await _api.getStation(id);
      _hasFood[id] = station.cateringStation;
      if (!_disposed) notifyListeners();
    } on ApiException {
      // Left out for now; asked again on the next update.
    } finally {
      _asking.remove(id);
    }
  }

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }

  /// The calling points strictly between the passenger's boarding station and
  /// destination (US-P8 AC1). Empty when either is unknown.
  static List<JourneyStop> between(JourneyDetail journey, String? boardingId, String? destinationId) {
    if (boardingId == null || destinationId == null) return const [];
    final boarding = journey.stopAt(boardingId);
    final destination = journey.stopAt(destinationId);
    if (boarding == null || destination == null) return const [];
    return [
      for (final s in journey.callingPoints)
        if (s.sequence > boarding.sequence && s.sequence < destination.sequence) s,
    ];
  }
}
