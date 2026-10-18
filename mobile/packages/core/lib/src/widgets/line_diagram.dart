import 'package:flutter/material.dart';

import '../theme/tokens.dart';

/// A short piece of the line around the train: the train marker either at a
/// station (previous - this - next) or between two stations. The position is
/// given by the caller as the backend resolved it; this widget only draws it.
class LineDiagram extends StatelessWidget {
  /// The train is at [current]; [previous] and [next] are the neighbouring
  /// calling points (null at the ends of the line).
  const LineDiagram.atStation({super.key, this.previous, required String current, this.next})
      : from = null,
        to = null,
        progress = null,
        // ignore: prefer_initializing_formals
        _current = current;

  /// The train is [progress] (0.0 to 1.0) of the way from [from] to [to].
  const LineDiagram.between({super.key, required String this.from, required String this.to, required double this.progress})
      : previous = null,
        next = null,
        _current = null,
        assert(progress >= 0 && progress <= 1);

  final String? previous;
  final String? next;
  final String? from;
  final String? to;
  final double? progress;
  final String? _current;

  bool get _atStation => _current != null;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final small = Theme.of(context).textTheme.bodySmall!;
    final bold = Theme.of(context).textTheme.labelLarge!.copyWith(fontSize: textSmall);

    // Marker position along the line: 0.5 at a station (previous - this - next).
    final position = _atStation ? 0.5 : progress!;
    final label = _atStation
        ? 'Train at $_current'
        : 'Train between $from and $to';

    return Semantics(
      label: label,
      container: true,
      excludeSemantics: true,
      child: Column(
        children: [
          SizedBox(
            height: touchTarget,
            width: double.infinity,
            child: CustomPaint(
              painter: _LinePainter(
                position: position,
                passed: scheme.primary,
                ahead: scheme.outline,
                markerFill: scheme.primary,
                markerIcon: scheme.onPrimary,
                hollowFill: scheme.surface,
                // A train at the first station has nothing behind it, and vice versa.
                hasStart: _atStation ? previous != null : true,
                hasEnd: _atStation ? next != null : true,
              ),
            ),
          ),
          const SizedBox(height: spaceXs),
          if (_atStation)
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(child: Text(previous ?? '', style: small)),
                Expanded(child: Text(_current!, style: bold, textAlign: TextAlign.center)),
                Expanded(child: Text(next ?? '', style: small, textAlign: TextAlign.end)),
              ],
            )
          else
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [Text(from!, style: small), Text(to!, style: bold)],
            ),
        ],
      ),
    );
  }
}

class _LinePainter extends CustomPainter {
  _LinePainter({
    required this.position,
    required this.passed,
    required this.ahead,
    required this.markerFill,
    required this.markerIcon,
    required this.hollowFill,
    required this.hasStart,
    required this.hasEnd,
  });

  final double position;
  final Color passed;
  final Color ahead;
  final Color markerFill;
  final Color markerIcon;
  final Color hollowFill;
  final bool hasStart;
  final bool hasEnd;

  static const _nodeRadius = 6.0;
  static const _markerRadius = 14.0;

  @override
  void paint(Canvas canvas, Size size) {
    final y = size.height / 2;
    final left = _markerRadius;
    final right = size.width - _markerRadius;
    final x = left + (right - left) * position;

    final line = Paint()
      ..strokeWidth = 4
      ..strokeCap = StrokeCap.round;
    canvas.drawLine(Offset(left, y), Offset(x, y), line..color = passed);
    canvas.drawLine(Offset(x, y), Offset(right, y), line..color = ahead);

    // End nodes: filled = already behind the train, hollow = still ahead.
    if (hasStart) canvas.drawCircle(Offset(left, y), _nodeRadius, Paint()..color = passed);
    if (hasEnd) {
      canvas.drawCircle(Offset(right, y), _nodeRadius, Paint()..color = hollowFill);
      canvas.drawCircle(
        Offset(right, y),
        _nodeRadius,
        Paint()
          ..color = ahead
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2,
      );
    }

    // Train marker: a disc with a train glyph.
    canvas.drawCircle(Offset(x, y), _markerRadius, Paint()..color = markerFill);
    final glyph = TextPainter(
      text: TextSpan(
        text: String.fromCharCode(Icons.train.codePoint),
        style: TextStyle(
          fontFamily: Icons.train.fontFamily,
          package: Icons.train.fontPackage,
          fontSize: 18,
          color: markerIcon,
        ),
      ),
      textDirection: TextDirection.ltr,
    )..layout();
    glyph.paint(canvas, Offset(x - glyph.width / 2, y - glyph.height / 2));
  }

  @override
  bool shouldRepaint(_LinePainter old) =>
      old.position != position ||
      old.passed != passed ||
      old.ahead != ahead ||
      old.hasStart != hasStart ||
      old.hasEnd != hasEnd;
}
