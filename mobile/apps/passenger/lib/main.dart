import 'package:flutter/material.dart';
import 'package:smartrail_core/smartrail_core.dart';

import 'features/home/home_screen.dart';

void main() {
  runApp(const PassengerApp());
}

class PassengerApp extends StatelessWidget {
  const PassengerApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'SmartRail',
      // Light or dark follows the phone's setting (design handoff rule 10).
      theme: buildPassengerTheme(Brightness.light),
      darkTheme: buildPassengerTheme(Brightness.dark),
      themeMode: ThemeMode.system,
      home: const HomeScreen(),
    );
  }
}
