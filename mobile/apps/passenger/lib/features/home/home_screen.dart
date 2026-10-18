import 'package:flutter/material.dart';

/// Placeholder until the search screen (step 2) replaces it.
class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('SmartRail')),
      body: const Center(child: Text('Find a train')),
    );
  }
}
