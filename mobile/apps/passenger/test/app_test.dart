import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';
import 'package:smartrail_passenger/main.dart';

void main() {
  testWidgets('the app starts with the token theme and the home screen', (tester) async {
    await tester.pumpWidget(const PassengerApp());

    expect(find.text('Find a train'), findsOneWidget);
    final context = tester.element(find.text('Find a train'));
    expect(Theme.of(context).colorScheme.primary, lightColors.accent);
    expect(Theme.of(context).extension<StatusTheme>(), isNotNull);
  });

  testWidgets('dark mode follows the system setting', (tester) async {
    tester.platformDispatcher.platformBrightnessTestValue = Brightness.dark;
    addTearDown(tester.platformDispatcher.clearPlatformBrightnessTestValue);

    await tester.pumpWidget(const PassengerApp());

    final context = tester.element(find.text('Find a train'));
    expect(Theme.of(context).colorScheme.primary, darkColors.accent);
  });
}
