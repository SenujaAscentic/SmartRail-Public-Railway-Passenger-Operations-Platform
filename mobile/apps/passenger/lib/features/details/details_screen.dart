import 'dart:async';

import 'package:flutter/material.dart';
import 'package:smartrail_core/smartrail_core.dart';

import 'details_controller.dart';
import 'train_set_chips.dart';

/// US-P2: one train with all its calling points, where it is now, and today's
/// train set. [boardingStationId] and [destinationStationId] (from the search)
/// mark "Your station" and "destination" on the list. In this step the data is
/// refreshed by hand; the live stream comes next.
class DetailsScreen extends StatefulWidget {
  const DetailsScreen({
    super.key,
    required this.controller,
    this.boardingStationId,
    this.destinationStationId,
  });

  final DetailController controller;
  final String? boardingStationId;
  final String? destinationStationId;

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

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        actions: [
          IconButton(tooltip: Strings.refresh, icon: const Icon(Icons.refresh), onPressed: _c.refresh),
        ],
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
                boardingStationId: widget.boardingStationId,
                destinationStationId: widget.destinationStationId,
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
  });

  final JourneyDetail journey;
  final Duration? age;
  final bool refreshFailed;
  final String? boardingStationId;
  final String? destinationStationId;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final points = journey.callingPoints;
    final position = _Position.of(journey, points);

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
        Center(child: StatusBadge.journey(journey.status)),
        if (position != null) ...[
          const SizedBox(height: spaceSm),
          Center(child: _PositionChip(text: position.text)),
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

/// Where the train is, drawn from what the backend sent (never computed here):
/// at a station when a stop is "Arrived", otherwise between the last and next
/// station of the last position. Only while the data is live or stale; when it
/// is lost or the train has not started, a position would mislead.
class _Position {
  const _Position(this.text, this.diagram);

  final String text;
  final LineDiagram diagram;

  static _Position? of(JourneyDetail journey, List<JourneyStop> points) {
    final tracking = journey.freshness.tracking;
    if (tracking != Tracking.live && tracking != Tracking.stale) return null;

    final arrivedIndex = points.indexWhere((s) => s.state == StopState.arrived);
    if (arrivedIndex >= 0) {
      final here = points[arrivedIndex].name;
      return _Position(
        Strings.atStation(here),
        LineDiagram.atStation(
          previous: arrivedIndex > 0 ? points[arrivedIndex - 1].name : null,
          current: here,
          next: arrivedIndex < points.length - 1 ? points[arrivedIndex + 1].name : null,
        ),
      );
    }

    final p = journey.lastPosition;
    if (p == null) return null;
    final last = journey.stopAt(p.lastStationId);
    final next = journey.stopAt(p.nextStationId);
    if (last == null || next == null || p.fractionToNext < 0 || p.fractionToNext > 1) return null;
    return _Position(
      Strings.betweenStations(last.name, next.name),
      LineDiagram.between(from: last.name, to: next.name, progress: p.fractionToNext),
    );
  }
}

class _PositionChip extends StatelessWidget {
  const _PositionChip({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    final color = Theme.of(context).colorScheme.primary;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: spaceMd, vertical: spaceSm),
      decoration: BoxDecoration(color: color.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(radiusMd)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          ExcludeSemantics(child: Icon(Icons.place, size: 18, color: color)),
          const SizedBox(width: spaceXs),
          Flexible(
            child: Text(text, style: Theme.of(context).textTheme.labelLarge!.copyWith(color: color, fontSize: textSmall)),
          ),
        ],
      ),
    );
  }
}
