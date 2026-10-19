import 'dart:io';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show FontLoader;
import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

/// Loads the bundled Noto fonts, so widths in tests match the real app
/// (the default test font makes every letter as wide as it is tall).
/// flutter test runs with the package folder as working directory.
Future<void> loadAppFonts() async {
  Future<void> load(String family, List<String> files) async {
    final loader = FontLoader('packages/smartrail_core/$family');
    for (final f in files) {
      final bytes = File('fonts/$f').readAsBytesSync();
      loader.addFont(Future.value(ByteData.sublistView(bytes)));
    }
    await loader.load();
  }

  await load('NotoSans', ['NotoSans-Regular.ttf', 'NotoSans-Bold.ttf']);
  await load('NotoSansMono', ['NotoSansMono-Regular.ttf', 'NotoSansMono-Bold.ttf']);
}

/// Shows [child] on a phone-sized screen with the passenger theme.
Future<void> pumpThemed(WidgetTester tester, Widget child, {Brightness brightness = Brightness.light}) async {
  tester.view.physicalSize = const Size(390 * 2, 844 * 2);
  tester.view.devicePixelRatio = 2;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    MaterialApp(
      theme: buildPassengerTheme(brightness),
      home: Scaffold(body: SingleChildScrollView(child: Padding(padding: const EdgeInsets.all(16), child: child))),
    ),
  );
}

/// The colours the widget should use for a token, in the given brightness.
StatusColors tokenColors(String name, Brightness brightness) {
  final t = statusTokens[name]!;
  return brightness == Brightness.dark ? t.dark : t.light;
}
