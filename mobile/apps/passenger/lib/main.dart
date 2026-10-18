import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:smartrail_core/smartrail_core.dart';

import 'config.dart';
import 'features/search/search_controller.dart' as search;
import 'features/search/search_screen.dart';

void main() {
  runApp(PassengerApp(api: SmartRailApi(http.Client(), apiBaseUrl), nowUtc: () => DateTime.timestamp()));
}

/// The api and the clock are passed in (constructor injection, ADR-12), so
/// tests run the whole app against a fake backend and a fixed "now".
class PassengerApp extends StatefulWidget {
  const PassengerApp({super.key, required this.api, required this.nowUtc});

  final SmartRailApi api;
  final DateTime Function() nowUtc;

  @override
  State<PassengerApp> createState() => _PassengerAppState();
}

class _PassengerAppState extends State<PassengerApp> {
  late final _search = search.SearchController(widget.api, widget.nowUtc);

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'SmartRail',
      // Light or dark follows the phone's setting (design handoff rule 10).
      theme: buildPassengerTheme(Brightness.light),
      darkTheme: buildPassengerTheme(Brightness.dark),
      themeMode: ThemeMode.system,
      home: SearchScreen(controller: _search, api: widget.api),
    );
  }
}
