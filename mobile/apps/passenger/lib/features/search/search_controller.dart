import 'package:flutter/foundation.dart';
import 'package:smartrail_core/smartrail_core.dart';

/// What the search screen shows below the form (ADR-12: a sealed class and a switch).
sealed class SearchState {
  const SearchState();
}

/// Nothing searched yet, or the form changed since the last search.
class SearchIdle extends SearchState {
  const SearchIdle();
}

class SearchLoading extends SearchState {
  const SearchLoading();
}

class SearchResults extends SearchState {
  const SearchResults(this.journeys);
  final List<JourneySummary> journeys;
}

/// The search worked and no direct train runs (US-P1 AC5).
class SearchEmpty extends SearchState {
  const SearchEmpty();
}

class SearchFailed extends SearchState {
  const SearchFailed();
}

/// Holds the search form and the result of the last search (US-P1).
/// It only passes the backend's answer on: no sorting, filtering or status logic
/// (the backend sorts by departure and hides past departures).
class SearchController extends ChangeNotifier {
  SearchController(this._api, this._nowUtc) : _date = formatDate(_nowUtc());

  final SmartRailApi _api;

  /// Injected so tests control "today" (CLAUDE.md: no DateTime.now in logic).
  final DateTime Function() _nowUtc;

  Station? _from;
  Station? _to;
  String _date;
  SearchState _state = const SearchIdle();

  /// Counts searches so that a slow, older answer cannot replace a newer one.
  int _searchId = 0;

  Station? get from => _from;
  Station? get to => _to;

  /// The chosen service date, `YYYY-MM-DD`; today in Colombo at the start.
  String get date => _date;
  SearchState get state => _state;

  /// Both stations are chosen and they differ.
  bool get canSearch => _from != null && _to != null && _from!.id != _to!.id;

  /// First day that can be chosen: today in Colombo.
  String get today => formatDate(_nowUtc());

  void setFrom(Station? station) => _changeForm(() => _from = station);
  void setTo(Station? station) => _changeForm(() => _to = station);
  void setDate(String isoDate) => _changeForm(() => _date = isoDate);

  /// Type-ahead for the station fields; a failure just means "no suggestions".
  Future<List<Station>> suggestions(String query) async {
    try {
      return await _api.searchStations(query);
    } on ApiException {
      return const [];
    }
  }

  Future<void> search() async {
    if (!canSearch) return;
    final id = ++_searchId;
    _state = const SearchLoading();
    notifyListeners();
    SearchState result;
    try {
      final journeys = await _api.searchJourneys(fromId: _from!.id, toId: _to!.id, date: _date);
      result = journeys.isEmpty ? const SearchEmpty() : SearchResults(journeys);
    } on ApiException {
      result = const SearchFailed();
    }
    if (id != _searchId) return; // a newer search or form change took over
    _state = result;
    notifyListeners();
  }

  // Results belong to the form values they were searched with, so changing the
  // form clears them instead of leaving an answer for other stations on screen.
  void _changeForm(void Function() change) {
    change();
    _searchId++;
    _state = const SearchIdle();
    notifyListeners();
  }
}
