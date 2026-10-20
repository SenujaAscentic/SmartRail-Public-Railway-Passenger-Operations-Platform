import 'package:flutter/material.dart';
import 'package:smartrail_core/smartrail_core.dart';

import 'catering_controller.dart';
import 'phone_launcher.dart';

/// US-P8: the menu preview of one station, and "Call to order". The passenger
/// orders by phone with the provider; there is no ordering in the app.
class CateringScreen extends StatefulWidget {
  const CateringScreen({
    super.key,
    required this.controller,
    required this.stationName,
    required this.serviceNumber,
    required this.arrival,
    required this.launchPhone,
  });

  final CateringController controller;
  final String stationName;

  /// For "Mention: Train 1005 · arriving ...".
  final String serviceNumber;

  /// When the train is expected at this station, as the backend sent it (null: the summary leaves the time out).
  final DateTime? arrival;
  final PhoneLauncher launchPhone;

  @override
  State<CateringScreen> createState() => _CateringScreenState();
}

class _CateringScreenState extends State<CateringScreen> {
  CateringController get _c => widget.controller;

  @override
  void initState() {
    super.initState();
    _c.load();
  }

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  Future<void> _call(String hotline) async {
    final messenger = ScaffoldMessenger.of(context);
    var opened = false;
    try {
      opened = await widget.launchPhone(Uri(scheme: 'tel', path: hotline));
    } on Exception {
      opened = false;
    }
    if (!opened) messenger.showSnackBar(const SnackBar(content: Text(Strings.couldNotCall)));
  }


  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(Strings.foodAt(widget.stationName))),
      // "Call to order" and what to say on the phone stay at the bottom, inside the safe area, while the menu scrolls. Only when
      // the menu is shown and there is a hotline (an empty hotline: no button); the button is enabled whether the hotline is open or not.
      bottomNavigationBar: ListenableBuilder(
        listenable: _c,
        builder: (context, _) => switch (_c.state) {
          CateringLoaded(:final catering) when catering.hasHotline => _CallBar(
              hotline: catering.hotline,
              mention: Strings.mentionOnCall(
                widget.serviceNumber,
                widget.stationName,
                widget.arrival == null ? null : formatTime(widget.arrival!),
              ),
              onCall: _call,
            ),
          _ => const SizedBox.shrink(),
        },
      ),
      body: ListenableBuilder(
        listenable: _c,
        builder: (context, _) => switch (_c.state) {
          CateringLoading() => const Center(child: CircularProgressIndicator()),
          CateringUnavailable() => ErrorState(message: Strings.menuUnavailable, retryLabel: Strings.tryAgain, onRetry: _c.load),
          CateringLoaded(:final catering) => _Loaded(catering: catering),
        },
      ),
    );
  }
}

/// The pinned bottom bar: what to mention on the call, and "Call to order".
class _CallBar extends StatelessWidget {
  const _CallBar({required this.hotline, required this.mention, required this.onCall});

  final String hotline;
  final String mention;
  final ValueChanged<String> onCall;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Material(
      color: theme.colorScheme.surface,
      elevation: 3,
      child: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(spaceMd),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              // What to say on the phone (US-P8 AC4).
              Text(mention, textAlign: TextAlign.center, style: theme.textTheme.bodyMedium),
              const SizedBox(height: spaceSm),
              SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  onPressed: () => onCall(hotline),
                  icon: const Icon(Icons.call),
                  label: const Text(Strings.callToOrder),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The header (a food icon in a circle, the caterer, its opening hours), then the menu.
class _Loaded extends StatelessWidget {
  const _Loaded({required this.catering});

  final StationCatering catering;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final hours = catering.openingHours;
    // Whether it is open now is the server's `openNow`; no clock in the app. The message needs the hours to say when it opens.
    final closed = !catering.openNow && hours != null;

    return ListView(
      padding: const EdgeInsets.all(spaceMd),
      children: [
        Center(
          child: ExcludeSemantics(
            child: CircleAvatar(
              radius: 44,
              backgroundColor: theme.colorScheme.primary.withValues(alpha: 0.12),
              child: Icon(Icons.restaurant, size: 48, color: theme.colorScheme.primary),
            ),
          ),
        ),
        const SizedBox(height: spaceMd),
        Text(Strings.catererName, textAlign: TextAlign.center, style: theme.textTheme.titleLarge),
        if (hours != null) ...[
          const SizedBox(height: spaceXs),
          Text(Strings.openingHours(hours.opens, hours.closes), textAlign: TextAlign.center, style: theme.textTheme.bodyMedium),
        ],
        if (closed) ...[
          const SizedBox(height: spaceSm),
          // Icon and words, not colour alone.
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              ExcludeSemantics(child: Icon(Icons.schedule, size: 20, color: theme.colorScheme.error)),
              const SizedBox(width: spaceXs),
              Flexible(
                child: Text(
                  Strings.hotlineClosed(hours.opens),
                  style: theme.textTheme.titleSmall!.copyWith(color: theme.colorScheme.error),
                ),
              ),
            ],
          ),
        ],
        const SizedBox(height: spaceLg),
        Text(Strings.menuHeading, style: theme.textTheme.titleMedium),
        const SizedBox(height: spaceXs),
        for (final item in catering.menu) _MenuRow(item: item),
        const SizedBox(height: spaceLg),
        Text(Strings.inAppOrderingSoon, textAlign: TextAlign.center, style: theme.textTheme.bodySmall),
        const SizedBox(height: spaceXs),
        Text(Strings.ordersHandledBy, textAlign: TextAlign.center, style: theme.textTheme.bodySmall),
      ],
    );
  }
}

class _MenuRow extends StatelessWidget {
  const _MenuRow({required this.item});

  final MenuItem item;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final muted = theme.colorScheme.onSurfaceVariant;
    return Container(
      constraints: const BoxConstraints(minHeight: touchTarget),
      padding: const EdgeInsets.symmetric(vertical: spaceSm),
      decoration: BoxDecoration(border: Border(bottom: BorderSide(color: theme.colorScheme.outlineVariant))),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(item.name, style: theme.textTheme.bodyLarge!.copyWith(color: item.available ? null : muted)),
              ),
              const SizedBox(width: spaceMd),
              Text(Strings.price(item.price), style: theme.textTheme.bodyLarge),
            ],
          ),
          const SizedBox(height: spaceXs),
          // Icon + words: available or sold out is never shown by colour alone.
          Row(
            children: [
              ExcludeSemantics(child: Icon(item.available ? Icons.check_circle_outline : Icons.block, size: 18, color: muted)),
              const SizedBox(width: spaceXs),
              Text(item.available ? Strings.available : Strings.soldOut, style: theme.textTheme.bodyMedium!.copyWith(color: muted)),
            ],
          ),
        ],
      ),
    );
  }
}
