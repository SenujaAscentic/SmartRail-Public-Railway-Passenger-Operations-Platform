import 'package:flutter/material.dart';
import 'package:smartrail_core/smartrail_core.dart';

import 'details_controller.dart';

// Pieces that the train details screen and the tracking screen both show.

/// Where the train is, drawn from what the backend sent (never computed here):
/// at a station when a stop is "Arrived", otherwise between the last and next
/// station of the last position. Only while the data is live or stale; when it
/// is lost or the train has not started, a position would mislead.
class TrainPosition {
  const TrainPosition(this.text, this.diagram);

  final String text;
  final LineDiagram diagram;

  static TrainPosition? of(JourneyDetail journey) {
    final tracking = journey.freshness.tracking;
    if (tracking != Tracking.live && tracking != Tracking.stale) return null;
    final points = journey.callingPoints;

    final arrivedIndex = points.indexWhere((s) => s.state == StopState.arrived);
    if (arrivedIndex >= 0) {
      final here = points[arrivedIndex].name;
      return TrainPosition(
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
    return TrainPosition(
      Strings.betweenStations(last.name, next.name),
      LineDiagram.between(from: last.name, to: next.name, progress: p.fractionToNext),
    );
  }
}

/// "Between Ragama and Gampaha" in a small rounded box with a pin.
class PositionChip extends StatelessWidget {
  const PositionChip({super.key, required this.text});

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

/// The controller's reasons, as lines of text: the reason of the delay or
/// cancellation, and the reason of a hold, when sent.
List<String> reasonTexts(JourneyDetail journey) => [
      if (journey.reason != null) Strings.reasonLine(journey.reason!.code, journey.reason!.note),
      if (journey.hold?.reason != null) Strings.reasonLine(journey.hold!.reason!.code, journey.hold!.reason!.note),
    ];

/// One line of plain text with an info icon, for the controller's reasons.
class ReasonLine extends StatelessWidget {
  const ReasonLine({super.key, required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        ExcludeSemantics(child: Icon(Icons.info_outline, size: 18, color: theme.colorScheme.onSurfaceVariant)),
        const SizedBox(width: spaceXs),
        // Plain text only: a Text widget never interprets markup.
        Flexible(child: Text(text, textAlign: TextAlign.center, style: theme.textTheme.bodySmall)),
      ],
    );
  }
}

/// "Next trains" / "Other trains": up to three journeys from the search, as the
/// backend sent them. The app only shows them.
class AlternativesSection extends StatelessWidget {
  const AlternativesSection({super.key, required this.state, required this.onRetry, required this.onOpen});

  final AlternativesState state;
  final VoidCallback onRetry;
  final ValueChanged<JourneySummary> onOpen;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    switch (state) {
      case NoAlternatives():
        return const SizedBox.shrink();
      case AlternativesLoading():
        return const Center(child: Padding(padding: EdgeInsets.all(spaceMd), child: CircularProgressIndicator()));
      case AlternativesFailed():
        return Column(
          children: [
            Text(Strings.otherTrainsError, style: theme.textTheme.bodyLarge),
            const SizedBox(height: spaceSm),
            OutlinedButton(onPressed: onRetry, child: const Text(Strings.tryAgain)),
          ],
        );
      case AlternativesLoaded(:final trains, :final kind):
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              kind == AlternativesKind.next ? Strings.nextTrains : Strings.otherTrains,
              style: theme.textTheme.titleMedium,
            ),
            const SizedBox(height: spaceSm),
            if (trains.isEmpty) Text(Strings.noOtherTrains, style: theme.textTheme.bodyLarge),
            for (final j in trains) ...[
              JourneyCard(
                serviceNumber: j.serviceNumber,
                name: j.name,
                status: j.shownStatus,
                scheduledDeparture: j.from.scheduledDeparture,
                scheduledArrival: j.to.scheduledArrival,
                expectedDeparture: j.from.expectedDeparture,
                expectedArrival: j.to.expectedArrival,
                platform: j.from.platform,
                previousPlatform: j.from.previousPlatform,
                onTap: () => onOpen(j),
              ),
              const SizedBox(height: spaceSm),
            ],
          ],
        );
    }
  }
}
