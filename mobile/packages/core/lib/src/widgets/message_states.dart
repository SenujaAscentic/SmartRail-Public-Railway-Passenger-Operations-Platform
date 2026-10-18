import 'package:flutter/material.dart';

import '../theme/tokens.dart';

/// A centred icon and message, with an optional action button (48 px high).
/// The screens pass the wording (see strings.dart).
class EmptyState extends StatelessWidget {
  const EmptyState({super.key, required this.message, this.icon = Icons.search_off, this.actionLabel, this.onAction})
      : assert((actionLabel == null) == (onAction == null), 'give both actionLabel and onAction, or neither');

  final String message;
  final IconData icon;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(spaceLg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ExcludeSemantics(child: Icon(icon, size: 48, color: scheme.onSurfaceVariant)),
            const SizedBox(height: spaceMd),
            Text(message, textAlign: TextAlign.center, style: Theme.of(context).textTheme.bodyLarge),
            if (actionLabel != null) ...[
              const SizedBox(height: spaceMd),
              OutlinedButton(onPressed: onAction, child: Text(actionLabel!)),
            ],
          ],
        ),
      ),
    );
  }
}

/// A failed request: the same layout as [EmptyState] with an error icon and a
/// retry button. The message must say what is wrong in words (never colour alone).
class ErrorState extends StatelessWidget {
  const ErrorState({super.key, required this.message, required this.retryLabel, required this.onRetry});

  final String message;
  final String retryLabel;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => EmptyState(
        message: message,
        icon: Icons.error_outline,
        actionLabel: retryLabel,
        onAction: onRetry,
      );
}
