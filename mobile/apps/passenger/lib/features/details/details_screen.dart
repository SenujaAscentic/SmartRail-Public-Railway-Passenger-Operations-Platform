import 'dart:async';

import 'package:flutter/material.dart';
import 'package:smartrail_core/smartrail_core.dart';

import '../tracking/tracking_screen.dart';
import 'details_controller.dart';
import 'journey_parts.dart';
import 'train_set_chips.dart';

/// US-P2: one train with all its calling points, where it is now, and today's
/// train set, kept up to date by the live stream (the controller). The
/// passenger's stations (from the search, held by the controller) mark "Your
/// station" and "destination". When the train is cancelled or ends early, other
/// trains are offered; [onOpenJourney] opens one of them.
class DetailsScreen extends StatefulWidget {
  const DetailsScreen({super.key, required this.controller, required this.onOpenJourney});

  final DetailController controller;
  final ValueChanged<JourneySummary> onOpenJourney;

  @override
  State<DetailsScreen> createState() => _DetailsScreenState();
}

class _DetailsScreenState extends State<DetailsScreen> {
  DetailController get _c => widget.controller;
  Timer? _tick;

  @override
  void initState() {
    super.initState();
    _c.load();
    // Redraw every second so "Updated 30 s ago" keeps counting.
    _tick = Timer.periodic(const Duration(seconds: 1), (_) => setState(() {}));
  }

  @override
  void dispose() {
    _tick?.cancel();
    _c.dispose();
    super.dispose();
  }

  void _openTracking() {
    // The tracking screen shows the same journey, kept up to date by the same
    // controller; this screen keeps owning it.
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => TrackingScreen(controller: _c, onOpenJourney: widget.onOpenJourney),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        actions: [
          IconButton(tooltip: Strings.refresh, icon: const Icon(Icons.refresh), onPressed: _c.refresh),
        ],
      ),
      // "Track this train" (US-P3): only when the passenger's stations are known
      // and the train still runs (not cancelled, not finished).
      bottomNavigationBar: ListenableBuilder(
        listenable: _c,
        builder: (context, _) {
          final state = _c.state;
          if (state is! DetailLoaded || _c.boardingStationId == null || _c.destinationStationId == null) {
            return const SizedBox.shrink();
          }
          final kind = state.journey.status.kind;
          if (kind == StatusKind.cancelled || kind == StatusKind.completed) return const SizedBox.shrink();
          return SafeArea(
            child: Padding(
              padding: const EdgeInsets.all(spaceMd),
              child: FilledButton(onPressed: _openTracking, child: const Text(Strings.trackThisTrain)),
            ),
          );
        },
      ),
      body: ListenableBuilder(
        listenable: _c,
        builder: (context, _) => switch (_c.state) {
          DetailLoading() => const Center(child: CircularProgressIndicator()),
          DetailFailed() => ErrorState(message: Strings.loadError, retryLabel: Strings.tryAgain, onRetry: _c.load),
          DetailLoaded(:final journey, :final refreshFailed) => RefreshIndicator(
              onRefresh: _c.refresh,
              child: _Body(
                journey: journey,
                age: _c.age,
                refreshFailed: refreshFailed,
                boardingStationId: _c.boardingStationId,
                destinationStationId: _c.destinationStationId,
                alternatives: _c.alternatives,
                onRetryAlternatives: _c.retryAlternatives,
                onOpenJourney: widget.onOpenJourney,
              ),
            ),
        },
      ),
    );
  }
}

class _Body extends StatelessWidget {
  const _Body({
    required this.journey,
    required this.age,
    required this.refreshFailed,
    required this.boardingStationId,
    required this.destinationStationId,
    required this.alternatives,
    required this.onRetryAlternatives,
    required this.onOpenJourney,
  });

  final JourneyDetail journey;
  final Duration? age;
  final bool refreshFailed;
  final String? boardingStationId;
  final String? destinationStationId;
  final AlternativesState alternatives;
  final VoidCallback onRetryAlternatives;
  final ValueChanged<JourneySummary> onOpenJourney;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final points = journey.callingPoints;
    final position = TrainPosition.of(journey);

    // The platform change of the passenger's own station, also in the header.
    final boardingStop = boardingStationId == null ? null : journey.stopAt(boardingStationId!);
    final boardingPlatformChange = (boardingStop != null && boardingStop.platform != null && boardingStop.previousPlatform != null)
        ? StatusBadge.platformChange(boardingStop.platform!, boardingStop.previousPlatform!)
        : null;

    // Why: the reason of the delay or cancellation, and the reason of a hold, when sent.
    final reasonLines = reasonTexts(journey);

    return ListView(
      // Lets a short list still be pulled down to refresh.
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(spaceMd),
      children: [
        Center(
          child: ExcludeSemantics(
            child: CircleAvatar(
              radius: 44,
              backgroundColor: theme.colorScheme.primary.withValues(alpha: 0.12),
              child: Icon(Icons.train, size: 48, color: theme.colorScheme.primary),
            ),
          ),
        ),
        const SizedBox(height: spaceMd),
        Text(journey.name, textAlign: TextAlign.center, style: theme.textTheme.titleLarge),
        const SizedBox(height: spaceXs),
        Text(
          Strings.trainLine(journey.serviceNumber, journey.from.name, journey.to.name),
          textAlign: TextAlign.center,
          style: theme.textTheme.bodySmall,
        ),
        const SizedBox(height: spaceSm),
        Center(child: StatusBadge.journey(journey.status, terminatesAt: journey.terminatesAt?.name)),
        if (boardingPlatformChange != null) ...[
          const SizedBox(height: spaceSm),
          Center(child: boardingPlatformChange),
        ],
        for (final line in reasonLines) ...[
          const SizedBox(height: spaceSm),
          ReasonLine(text: line),
        ],
        if (position != null) ...[
          const SizedBox(height: spaceSm),
          Center(child: PositionChip(text: position.text)),
        ],
        const SizedBox(height: spaceSm),
        Center(child: AgeLabel(freshness: journey.freshness, age: age)),
        const SizedBox(height: spaceSm),
        // Stale / lost banner from the tracking state the backend sent.
        FreshnessBanner(freshness: journey.freshness),
        if (refreshFailed && age != null) ...[
          const SizedBox(height: spaceSm),
          FreshnessBanner.offline(age: age!),
        ],
        if (position != null) ...[
          const SizedBox(height: spaceMd),
          position.diagram,
        ],
        if (alternatives is! NoAlternatives) ...[
          const SizedBox(height: spaceMd),
          AlternativesSection(state: alternatives, onRetry: onRetryAlternatives, onOpen: onOpenJourney),
        ],
        const SizedBox(height: spaceMd),
        TrainSetChips(trainSet: journey.trainSet),
        const SizedBox(height: spaceMd),
        const Divider(height: 1),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: spaceMd, vertical: spaceSm),
          child: Row(
            children: [
              Expanded(child: Text(Strings.stationColumn, style: theme.textTheme.bodySmall)),
              Text(Strings.scheduledColumn, style: theme.textTheme.bodySmall),
              const SizedBox(width: spaceSm),
              SizedBox(
                width: 56,
                child: Text(Strings.expectedColumn, textAlign: TextAlign.end, style: theme.textTheme.bodySmall),
              ),
            ],
          ),
        ),
        for (final stop in points) ..._row(stop),
      ],
    );
  }

  /// One calling point. The time shown is the arrival (departure at the origin
  /// and at the passenger's boarding station, where the passenger leaves), as
  /// scheduled, and as expected or, once it has happened, as it actually was.
  List<Widget> _row(JourneyStop stop) {
    final boarding = stop.stationId == boardingStationId;
    final useDeparture = boarding || stop.scheduledArrival == null;
    final scheduled = useDeparture ? stop.scheduledDeparture : stop.scheduledArrival;
    final shown = useDeparture
        ? (stop.actualDeparture ?? stop.expectedDeparture)
        : (stop.actualArrival ?? stop.expectedArrival);
    final time = scheduled ?? shown;
    if (time == null) return const []; // no time at all: nothing useful to show

    return [
      Padding(
        padding: const EdgeInsets.only(top: spaceXs),
        child: StopRow(
          name: stop.name,
          state: stop.state,
          scheduled: time,
          expected: shown,
          // The platform shows at the passenger's station; a changed platform shows anywhere.
          platform: (boarding || stop.previousPlatform != null) ? stop.platform : null,
          previousPlatform: stop.previousPlatform,
          isAdded: stop.isAdded,
          isYourStation: boarding,
          isDestination: stop.stationId == destinationStationId,
          journeyStatus: journey.status,
        ),
      ),
    ];
  }
}
