import 'package:flutter/material.dart';
import 'package:smartrail_core/smartrail_core.dart';

import '../details/details_controller.dart';
import '../details/details_screen.dart';
import 'search_controller.dart' as search;
import '../catering/phone_launcher.dart';
import 'station_field.dart';

/// US-P1: find the direct trains between two stations on a date.
class SearchScreen extends StatefulWidget {
  const SearchScreen({super.key, required this.controller, required this.api, this.launchPhone = openDialler, this.nowUtc});

  final search.SearchController controller;

  /// Used to open the train details of a result.
  final SmartRailApi api;

  /// Handed on to the train details (the menu screen: "Call to order", and its clock).
  final PhoneLauncher launchPhone;
  final DateTime Function()? nowUtc;

  @override
  State<SearchScreen> createState() => _SearchScreenState();
}

class _SearchScreenState extends State<SearchScreen> {
  search.SearchController get _c => widget.controller;

  /// How far ahead a date can be chosen (the date picker needs an end).
  static const _daysAhead = 60;

  void _open(JourneySummary journey) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => DetailsScreen(
          // "Your station" and "destination" on the stop list, and the alternatives.
          controller: DetailController(
            widget.api,
            journey.journeyId,
            boardingStationId: journey.from.stationId,
            destinationStationId: journey.to.stationId,
          ),
          onOpenJourney: _open,
          launchPhone: widget.launchPhone,
          nowUtc: widget.nowUtc,
        ),
      ),
    );
  }

  Future<void> _pickDate() async {
    final first = _toLocalDate(_c.today);
    final picked = await showDatePicker(
      context: context,
      initialDate: _toLocalDate(_c.date),
      firstDate: first,
      lastDate: first.add(const Duration(days: _daysAhead)),
    );
    if (picked != null) _c.setDate(_isoDate(picked));
  }

  // The date picker works with plain calendar dates. They are built from the
  // year / month / day only, never converted between time zones.
  static DateTime _toLocalDate(String iso) {
    final p = iso.split('-').map(int.parse).toList();
    return DateTime(p[0], p[1], p[2]);
  }

  static String _isoDate(DateTime d) =>
      '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text(Strings.searchTitle)),
      body: ListenableBuilder(
        listenable: _c,
        builder: (context, _) => Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.all(spaceMd),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  StationField(label: Strings.from, suggestions: _c.suggestions, onChanged: _c.setFrom),
                  const SizedBox(height: spaceSm),
                  StationField(label: Strings.to, suggestions: _c.suggestions, onChanged: _c.setTo),
                  const SizedBox(height: spaceSm),
                  OutlinedButton.icon(
                    onPressed: _pickDate,
                    icon: const Icon(Icons.calendar_today),
                    label: Text('${Strings.date}: ${dateLabel(_c.date)}'),
                  ),
                  const SizedBox(height: spaceSm),
                  FilledButton(onPressed: _c.canSearch ? _c.search : null, child: const Text(Strings.search)),
                ],
              ),
            ),
            Expanded(child: _Results(state: _c.state, onRetry: _c.search, onOpen: _open)),
          ],
        ),
      ),
    );
  }
}

class _Results extends StatelessWidget {
  const _Results({required this.state, required this.onRetry, required this.onOpen});

  final search.SearchState state;
  final VoidCallback onRetry;
  final ValueChanged<JourneySummary> onOpen;

  @override
  Widget build(BuildContext context) {
    return switch (state) {
      search.SearchIdle() => const SizedBox.shrink(),
      search.SearchLoading() => const Center(child: CircularProgressIndicator()),
      search.SearchEmpty() => const EmptyState(message: Strings.noDirectTrains),
      search.SearchFailed() => ErrorState(message: Strings.loadError, retryLabel: Strings.tryAgain, onRetry: onRetry),
      search.SearchResults(:final journeys) => ListView.separated(
          padding: const EdgeInsets.all(spaceMd),
          itemCount: journeys.length,
          separatorBuilder: (_, _) => const SizedBox(height: spaceSm),
          itemBuilder: (context, i) {
            final j = journeys[i];
            return JourneyCard(
              serviceNumber: j.serviceNumber,
              name: j.name,
              status: j.status,
              scheduledDeparture: j.from.scheduledDeparture,
              scheduledArrival: j.to.scheduledArrival,
              expectedDeparture: j.from.expectedDeparture,
              expectedArrival: j.to.expectedArrival,
              platform: j.from.platform,
              previousPlatform: j.from.previousPlatform,
              onTap: () => onOpen(j),
            );
          },
        ),
    };
  }
}
