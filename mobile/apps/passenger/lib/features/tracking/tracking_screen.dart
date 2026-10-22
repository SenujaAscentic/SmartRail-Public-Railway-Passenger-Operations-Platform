import 'dart:async';

import 'package:flutter/material.dart';
import 'package:smartrail_core/smartrail_core.dart';

import '../details/details_controller.dart';
import '../details/journey_parts.dart';
import 'tracking_facts.dart';

/// What the passenger is doing: still waiting at the station, or on the train.
/// The passenger chooses it; the backend knows nothing about it.
enum TrackingMode { waiting, onBoard }

/// US-P3: follow one train while waiting for it and while travelling.
/// It shows the same journey as the details screen and uses the same controller
/// (live stream, age, alternatives); this screen does not own it.
///
/// Waiting: where the train is and when it is expected at the boarding station.
/// On board: the next stop and the expected arrival at the destination. When the
/// train has left the boarding station and the passenger has not said anything,
/// "Are you on board?" is asked once: yes switches to on board, "No, I missed
/// it" shows the next trains to the same destination.
class TrackingScreen extends StatefulWidget {
  const TrackingScreen({super.key, required this.controller, required this.onOpenJourney});

  final DetailController controller;
  final ValueChanged<JourneySummary> onOpenJourney;

  @override
  State<TrackingScreen> createState() => _TrackingScreenState();
}

class _TrackingScreenState extends State<TrackingScreen> {
  DetailController get _c => widget.controller;

  TrackingMode _mode = TrackingMode.waiting;

  /// "Are you on board?" is asked once per visit.
  bool _asked = false;
  Timer? _tick;

  @override
  void initState() {
    super.initState();
    _c.addListener(_maybeAsk);
    // Redraw every second so "Updated 30 s ago" keeps counting.
    _tick = Timer.periodic(const Duration(seconds: 1), (_) => setState(() {}));
    WidgetsBinding.instance.addPostFrameCallback((_) => _maybeAsk());
  }

  @override
  void dispose() {
    _tick?.cancel();
    _c.removeListener(_maybeAsk);
    super.dispose();
  }

  void _maybeAsk() {
    if (!mounted || _asked || _mode != TrackingMode.waiting) return;
    final state = _c.state;
    if (state is! DetailLoaded) return;
    final boardingId = _c.boardingStationId;
    final boarding = boardingId == null ? null : state.journey.stopAt(boardingId);
    if (!hasLeft(boarding)) return;

    _asked = true;
    final journey = state.journey;
    final leftAt = passedAt(boarding!);
    // Not while the framework is building: ask after this frame.
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      if (!mounted) return;
      final onBoard = await showModalBottomSheet<bool>(
        context: context,
        builder: (context) => _AreYouOnBoard(
          text: Strings.trainLeft(journey.name, boarding.name, leftAt == null ? '' : formatTime(leftAt)),
        ),
      );
      if (!mounted || onBoard == null) return; // dismissed: nothing changes, not asked again
      if (onBoard) {
        setState(() => _mode = TrackingMode.onBoard);
      } else {
        _c.missedIt();
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return ListenableBuilder(
      listenable: _c,
      builder: (context, _) {
        final state = _c.state;
        final journey = state is DetailLoaded ? state.journey : null;
        return Scaffold(
          appBar: AppBar(
            title: journey == null
                ? null
                : Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(journey.name, style: theme.textTheme.titleLarge),
                      Text(
                        Strings.trainLine(journey.serviceNumber, journey.from.name, journey.to.name),
                        style: theme.textTheme.bodySmall,
                      ),
                    ],
                  ),
          ),
          body: switch (state) {
            DetailLoading() => const Center(child: CircularProgressIndicator()),
            DetailFailed() => ErrorState(message: Strings.loadError, retryLabel: Strings.tryAgain, onRetry: _c.load),
            DetailLoaded(:final journey, :final refreshFailed) => RefreshIndicator(
                onRefresh: _c.refresh,
                child: _content(journey, refreshFailed),
              ),
          },
          bottomNavigationBar: journey == null
              ? null
              : SafeArea(
                  child: Padding(
                    padding: const EdgeInsets.all(spaceMd),
                    child: _mode == TrackingMode.waiting
                        ? FilledButton(
                            onPressed: () => setState(() => _mode = TrackingMode.onBoard),
                            child: const Text(Strings.imOnThisTrain),
                          )
                        : OutlinedButton(
                            onPressed: () => Navigator.of(context).pop(),
                            child: const Text(Strings.stopTracking),
                          ),
                  ),
                ),
        );
      },
    );
  }

  Widget _content(JourneyDetail journey, bool refreshFailed) {
    final age = _c.age;
    final boardingId = _c.boardingStationId;
    final destinationId = _c.destinationStationId;
    final boarding = boardingId == null ? null : journey.stopAt(boardingId);
    final destination = destinationId == null ? null : journey.stopAt(destinationId);

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(spaceMd),
      children: [
        FreshnessBanner(freshness: journey.freshness),
        if (refreshFailed && age != null) ...[
          const SizedBox(height: spaceSm),
          FreshnessBanner.offline(age: age),
        ],
        const SizedBox(height: spaceSm),
        // Material 3 segmented control: the passenger can switch either way.
        SegmentedButton<TrackingMode>(
          segments: const [
            ButtonSegment(value: TrackingMode.waiting, label: Text(Strings.waiting)),
            ButtonSegment(value: TrackingMode.onBoard, label: Text(Strings.onBoard)),
          ],
          selected: {_mode},
          showSelectedIcon: false,
          onSelectionChanged: (selection) => setState(() => _mode = selection.first),
        ),
        const SizedBox(height: spaceMd),
        if (_mode == TrackingMode.waiting) ..._waiting(journey, boarding) else ..._onBoard(journey, destination),
        for (final line in reasonTexts(journey)) ...[
          const SizedBox(height: spaceSm),
          ReasonLine(text: line),
        ],
        const SizedBox(height: spaceMd),
        ..._whereIsTheTrain(journey, age),
        if (_c.alternatives is! NoAlternatives) ...[
          const SizedBox(height: spaceMd),
          AlternativesSection(state: _c.alternatives, onRetry: _c.retryAlternatives, onOpen: widget.onOpenJourney),
        ],
      ],
    );
  }

  /// Waiting: when the train is expected at the boarding station.
  List<Widget> _waiting(JourneyDetail journey, JourneyStop? boarding) {
    if (boarding == null) return const [];
    // The train will not serve the passenger's station (the journey is cancelled, or ends before it): no "expected" time, only the
    // journey's own status, "Cancelled" (S-06) or "Terminates at X" (S-07).
    if (journey.notServedBecauseOfJourney(boarding)) return [_JourneyStatusCard(journey: journey)];
    final times = arrivalTimes(boarding);
    // Before it arrives: expected. At the station: when it arrived. After: when it left.
    final (label, shown, showScheduled) = switch (boarding.state) {
      StopState.arrived => (Strings.arrivedAt(boarding.name), times.expected, false),
      StopState.departed || StopState.passed => (Strings.leftStation(boarding.name), passedAt(boarding), false),
      // A train that has not started has no live expected time: its time is the schedule, and it says so.
      _ => (
        journey.status.kind == StatusKind.notStarted ? Strings.scheduledAt(boarding.name) : Strings.expectedAt(boarding.name),
        times.expected,
        true,
      ),
    };
    final platformChanged = boarding.platform != null && boarding.previousPlatform != null;

    return [
      _BigTimeCard(
        label: label,
        time: shown ?? times.scheduled,
        scheduled: showScheduled ? times.scheduled : null,
        status: journey.shownStatus,
        side: boarding.platform == null ? null : _PlatformBox(platform: boarding.platform!),
      ),
      if (platformChanged) ...[
        const SizedBox(height: spaceSm),
        Align(
          alignment: Alignment.centerLeft,
          child: StatusBadge.platformChange(boarding.platform!, boarding.previousPlatform!),
        ),
      ],
    ];
  }

  /// On board: the next stop, and when the train arrives at the destination.
  List<Widget> _onBoard(JourneyDetail journey, JourneyStop? destination) {
    final next = nextCallingStop(journey);
    final reached = destination != null &&
        (destination.state == StopState.arrived || destination.state == StopState.departed || destination.state == StopState.passed);
    final times = destination == null ? null : arrivalTimes(destination);
    if (destination != null && journey.notServedBecauseOfJourney(destination)) return [_JourneyStatusCard(journey: journey)];
    return [
      if (next != null) _NextStopCard(stop: next),
      if (next != null) const SizedBox(height: spaceMd),
      if (destination != null && destination.state != StopState.skipped && times != null)
        _BigTimeCard(
          label: reached ? Strings.arrivedAt(destination.name) : Strings.arrivesAt(destination.name),
          time: times.expected ?? times.scheduled,
          scheduled: reached ? null : times.scheduled,
          status: journey.shownStatus,
        ),
    ];
  }

  /// The diagram of where the train is, "Last passed ... at ...", and the age.
  List<Widget> _whereIsTheTrain(JourneyDetail journey, Duration? age) {
    final theme = Theme.of(context);
    final position = TrainPosition.of(journey);
    final last = lastPassedStop(journey);
    final lastTime = last == null ? null : passedAt(last);
    return [
      Text(Strings.whereTheTrainIs, style: theme.textTheme.titleMedium),
      const SizedBox(height: spaceSm),
      if (position != null) ...[
        position.diagram,
        const SizedBox(height: spaceSm),
        PositionChip(text: position.text),
        const SizedBox(height: spaceSm),
      ],
      if (last != null && lastTime != null)
        Text(Strings.lastPassed(last.name, formatTime(lastTime)), style: theme.textTheme.bodyLarge),
      const SizedBox(height: spaceSm),
      AgeLabel(freshness: journey.freshness, age: age),
    ];
  }
}

/// A big expected time with the scheduled time struck through beside it, and the
/// journey status. Shows what was sent, no more.
class _BigTimeCard extends StatelessWidget {
  const _BigTimeCard({required this.label, required this.time, required this.scheduled, required this.status, this.side});

  final String label;
  final DateTime? time;

  /// Shown struck through, only when it differs from [time].
  final DateTime? scheduled;
  final JourneyStatus status;
  final Widget? side;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final differs = time != null && scheduled != null && formatTime(time!) != formatTime(scheduled!);
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(spaceMd),
      decoration: BoxDecoration(color: scheme.surfaceContainer, borderRadius: BorderRadius.circular(radiusLg)),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label, style: theme.textTheme.bodySmall),
                const SizedBox(height: spaceXs),
                Text(
                  time == null ? '--:--' : formatTime(time!),
                  style: monoStyle(context, size: 40, weight: FontWeight.w700),
                ),
                if (differs)
                  Text.rich(
                    TextSpan(children: [
                      TextSpan(text: '${Strings.scheduledLabel} ', style: monoStyle(context, size: textSmall).copyWith(color: scheme.onSurfaceVariant)),
                      TextSpan(
                        text: formatTime(scheduled!),
                        style: monoStyle(context, size: textSmall).copyWith(
                          color: scheme.onSurfaceVariant,
                          decoration: TextDecoration.lineThrough,
                        ),
                      ),
                    ]),
                  ),
                const SizedBox(height: spaceSm),
                StatusBadge.journey(status),
              ],
            ),
          ),
          if (side != null) ...[const SizedBox(width: spaceSm), side!],
        ],
      ),
    );
  }
}

/// The journey's own status where a time would be: used when the train will not serve the passenger's station.
class _JourneyStatusCard extends StatelessWidget {
  const _JourneyStatusCard({required this.journey});

  final JourneyDetail journey;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(spaceMd),
      decoration: BoxDecoration(color: scheme.surfaceContainer, borderRadius: BorderRadius.circular(radiusLg)),
      child: Align(
        alignment: Alignment.centerLeft,
        child: StatusBadge.journey(journey.shownStatus, terminatesAt: journey.terminatesAt?.name),
      ),
    );
  }
}

/// "Platform" and a big platform number in a bordered box.
class _PlatformBox extends StatelessWidget {
  const _PlatformBox({required this.platform});

  final String platform;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: spaceMd, vertical: spaceSm),
      decoration: BoxDecoration(
        color: scheme.surface,
        border: Border.all(color: scheme.outline),
        borderRadius: BorderRadius.circular(radiusLg),
      ),
      child: Column(
        children: [
          Text(Strings.platformWord, style: Theme.of(context).textTheme.bodySmall),
          Text(platform, style: monoStyle(context, size: textPlatform, weight: FontWeight.w700)),
        ],
      ),
    );
  }
}

/// "Next stop": the station, its platform when known, and the expected arrival.
class _NextStopCard extends StatelessWidget {
  const _NextStopCard({required this.stop});

  final JourneyStop stop;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final times = arrivalTimes(stop);
    final shown = times.expected ?? times.scheduled;
    final differs = shown != null && times.scheduled != null && formatTime(shown) != formatTime(times.scheduled!);
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(spaceMd),
      decoration: BoxDecoration(
        border: Border.all(color: scheme.outline),
        borderRadius: BorderRadius.circular(radiusLg),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(Strings.nextStop, style: theme.textTheme.bodySmall),
                Text(stop.name, style: theme.textTheme.titleLarge),
                if (stop.platform != null)
                  Text(platformText(stop.platform!, stop.previousPlatform), style: theme.textTheme.bodyLarge),
              ],
            ),
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(shown == null ? '--:--' : formatTime(shown), style: monoStyle(context, size: textTime, weight: FontWeight.w700)),
              if (differs)
                Text(
                  formatTime(times.scheduled!),
                  style: monoStyle(context, size: textSmall).copyWith(
                    color: scheme.onSurfaceVariant,
                    decoration: TextDecoration.lineThrough,
                  ),
                ),
            ],
          ),
        ],
      ),
    );
  }
}

/// The bottom sheet "Are you on board?" (E-24).
class _AreYouOnBoard extends StatelessWidget {
  const _AreYouOnBoard({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(spaceMd),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(text, style: theme.textTheme.bodySmall),
            const SizedBox(height: spaceXs),
            Text(Strings.areYouOnBoard, style: theme.textTheme.titleLarge),
            const SizedBox(height: spaceMd),
            FilledButton(onPressed: () => Navigator.of(context).pop(true), child: const Text(Strings.yesOnBoard)),
            const SizedBox(height: spaceSm),
            OutlinedButton(onPressed: () => Navigator.of(context).pop(false), child: const Text(Strings.noMissedIt)),
          ],
        ),
      ),
    );
  }
}
