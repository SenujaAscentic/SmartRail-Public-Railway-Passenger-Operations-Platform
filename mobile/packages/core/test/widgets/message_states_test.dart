import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

import '../support/pump.dart';

void main() {
  setUpAll(loadAppFonts);

  testWidgets('empty state: the "no direct trains" text and an icon', (tester) async {
    await pumpThemed(tester, const SizedBox(height: 400, child: EmptyState(message: Strings.noDirectTrains)));
    expect(find.text('No direct trains between these stations on this date'), findsOneWidget);
    expect(find.byIcon(Icons.search_off), findsOneWidget);
    expect(find.byType(OutlinedButton), findsNothing);
  });

  testWidgets('empty state with an action: the button works and is at least 48 px high', (tester) async {
    var taps = 0;
    await pumpThemed(
      tester,
      SizedBox(height: 400, child: EmptyState(message: 'Nothing here', actionLabel: 'Change date', onAction: () => taps++)),
    );
    await tester.tap(find.text('Change date'));
    expect(taps, 1);
    expect(tester.getSize(find.byType(OutlinedButton)).height, greaterThanOrEqualTo(48));
  });

  testWidgets('error state: message in words, error icon and a retry button', (tester) async {
    var retries = 0;
    await pumpThemed(
      tester,
      SizedBox(height: 400, child: ErrorState(message: 'Could not load', retryLabel: 'Try again', onRetry: () => retries++)),
    );
    expect(find.text('Could not load'), findsOneWidget);
    expect(find.byIcon(Icons.error_outline), findsOneWidget);
    await tester.tap(find.text('Try again'));
    expect(retries, 1);
  });

  test('an action label without a callback is refused', () {
    expect(() => EmptyState(message: 'x', actionLabel: 'Go'), throwsAssertionError);
  });
}
