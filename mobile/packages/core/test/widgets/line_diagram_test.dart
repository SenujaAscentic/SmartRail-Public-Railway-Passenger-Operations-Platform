import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

import '../support/pump.dart';

void main() {
  setUpAll(loadAppFonts);

  testWidgets('at a station: three names and a description for screen readers', (tester) async {
    final semantics = tester.ensureSemantics();
    await pumpThemed(tester, const LineDiagram.atStation(previous: 'Ragama', current: 'Gampaha', next: 'Veyangoda'));
    expect(find.text('Ragama'), findsOneWidget);
    expect(find.text('Gampaha'), findsOneWidget);
    expect(find.text('Veyangoda'), findsOneWidget);
    expect(find.bySemanticsLabel('Train at Gampaha'), findsOneWidget);
    semantics.dispose();
  });

  testWidgets('between stations: both names and a description', (tester) async {
    final semantics = tester.ensureSemantics();
    await pumpThemed(tester, const LineDiagram.between(from: 'Ragama', to: 'Gampaha', progress: 0.65));
    expect(find.text('Ragama'), findsOneWidget);
    expect(find.text('Gampaha'), findsOneWidget);
    expect(find.bySemanticsLabel('Train between Ragama and Gampaha'), findsOneWidget);
    semantics.dispose();
  });

  test('progress outside 0 to 1 is refused', () {
    expect(() => LineDiagram.between(from: 'A', to: 'B', progress: 1.5), throwsAssertionError);
  });

  testWidgets('a CustomPainter draws the line, and it repaints when the progress changes', (tester) async {
    await pumpThemed(tester, const LineDiagram.between(from: 'A', to: 'B', progress: 0.1));
    final first = tester.widget<CustomPaint>(find.descendant(of: find.byType(LineDiagram), matching: find.byType(CustomPaint)).first);
    await pumpThemed(tester, const LineDiagram.between(from: 'A', to: 'B', progress: 0.9));
    final second = tester.widget<CustomPaint>(find.descendant(of: find.byType(LineDiagram), matching: find.byType(CustomPaint)).first);
    expect(second.painter!.shouldRepaint(first.painter!), isTrue);
  });
}
