import 'package:flutter/material.dart';
import 'package:smartrail_core/smartrail_core.dart';

/// A text field with station type-ahead (US-P1 AC1). Typing asks the backend for
/// matching stations after a short pause; picking one calls [onChanged] with it,
/// and editing the text afterwards calls [onChanged] with null again.
class StationField extends StatefulWidget {
  const StationField({
    super.key,
    required this.label,
    required this.suggestions,
    required this.onChanged,
  });

  final String label;
  final Future<List<Station>> Function(String query) suggestions;
  final ValueChanged<Station?> onChanged;

  @override
  State<StationField> createState() => _StationFieldState();
}

class _StationFieldState extends State<StationField> {
  /// Wait for a pause in typing before calling the backend.
  static const _pause = Duration(milliseconds: 300);

  Station? _selected;

  /// Counts lookups, so that only the newest one shows its answer.
  int _lookup = 0;

  Future<Iterable<Station>> _options(TextEditingValue value) async {
    final query = value.text.trim();
    // Editing the text after a choice means that choice no longer applies.
    if (_selected != null && value.text != _selected!.name) {
      _selected = null;
      widget.onChanged(null);
    }
    if (query.isEmpty || query == _selected?.name) return const [];
    final id = ++_lookup;
    await Future<void>.delayed(_pause);
    if (id != _lookup) return const [];
    return widget.suggestions(query);
  }

  @override
  Widget build(BuildContext context) {
    return Autocomplete<Station>(
      displayStringForOption: (station) => station.name,
      optionsBuilder: _options,
      onSelected: (station) {
        _selected = station;
        widget.onChanged(station);
      },
      fieldViewBuilder: (context, controller, focusNode, onSubmitted) => TextField(
        controller: controller,
        focusNode: focusNode,
        textInputAction: TextInputAction.next,
        decoration: InputDecoration(labelText: widget.label, hintText: Strings.stationHint, border: const OutlineInputBorder()),
        onSubmitted: (_) => onSubmitted(),
      ),
    );
  }
}
