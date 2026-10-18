import 'package:flutter/material.dart';
import 'package:smartrail_core/smartrail_core.dart';

/// Today's train set as small labels: the traction and each class with its
/// seating ("1st class · reserved"). With no train set assigned yet it says
/// "Usually: power set" in muted text (US-P2 AC4).
class TrainSetChips extends StatelessWidget {
  const TrainSetChips({super.key, required this.trainSet});

  final TrainSet? trainSet;

  @override
  Widget build(BuildContext context) {
    final set = trainSet;
    if (set == null) {
      return Text(Strings.usuallyPowerSet, style: Theme.of(context).textTheme.bodySmall);
    }
    // One label per distinct class + seating, in coach order.
    final groups = <(String, String)>[];
    for (final coach in set.coaches) {
      final group = (coach.coachClass, coach.seating);
      if (!groups.contains(group)) groups.add(group);
    }
    return Wrap(
      alignment: WrapAlignment.center,
      spacing: spaceSm,
      runSpacing: spaceSm,
      children: [
        _Chip(bold: Strings.traction(set.traction)),
        for (final (coachClass, seating) in groups)
          _Chip(bold: Strings.coachClass(coachClass), rest: ' · ${Strings.seating(seating)}'),
      ],
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip({required this.bold, this.rest = ''});

  final String bold;
  final String rest;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: spaceMd, vertical: spaceSm),
      decoration: BoxDecoration(color: theme.colorScheme.surfaceContainer, borderRadius: BorderRadius.circular(radiusMd)),
      child: Text.rich(
        TextSpan(children: [
          TextSpan(text: bold, style: theme.textTheme.labelLarge!.copyWith(fontSize: textSmall)),
          TextSpan(text: rest, style: theme.textTheme.bodySmall!.copyWith(color: theme.colorScheme.onSurface)),
        ]),
      ),
    );
  }
}
