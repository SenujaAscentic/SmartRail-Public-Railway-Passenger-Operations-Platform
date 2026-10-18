import 'dart:convert';

import '../models/journey_detail.dart';
import 'sse_parser.dart';

/// What a stream frame means for the passenger app (API contract §4).
///
/// The app applies only a train position directly. Every other change (a delay,
/// a stop status, a hold, a cancellation, a type it has never seen) is a signal
/// to load the journey again: the app never works out business changes itself
/// (CLAUDE.md §3).
sealed class LiveMessage {
  const LiveMessage();
}

/// First frame after connecting: the state at stream sequence [sequence].
class SnapshotMessage extends LiveMessage {
  const SnapshotMessage(this.sequence);
  final int sequence;
}

/// The missed events are no longer kept: reload the journey.
class ResyncMessage extends LiveMessage {
  const ResyncMessage();
}

/// A new train position, applied directly (marker and age reset).
class PositionMessage extends LiveMessage {
  const PositionMessage({
    required this.sequence,
    required this.journeyId,
    required this.occurredAt,
    required this.position,
  });

  final int sequence;
  final String journeyId;
  final DateTime occurredAt;
  final Position position;

  /// Strict: a missing field throws, so a renamed field fails a test.
  factory PositionMessage.fromJson(Map<String, dynamic> json) => PositionMessage(
        sequence: json['sequence'] as int,
        journeyId: json['journeyId'] as String,
        occurredAt: DateTime.parse(json['occurredAt'] as String),
        position: Position.fromJson(json['payload'] as Map<String, dynamic>),
      );
}

/// Any other event: the journey changed in some way, load it again.
class ChangeMessage extends LiveMessage {
  const ChangeMessage({required this.type, this.sequence, this.journeyId});

  final String type;

  /// Null when the frame carried no readable sequence.
  final int? sequence;
  final String? journeyId;
}

/// Turns a frame into a message. Never throws: a frame it cannot read becomes a
/// [ChangeMessage], which is the safe reaction (load the journey again).
LiveMessage parseLiveFrame(SseFrame frame) {
  Map<String, dynamic>? json;
  try {
    final decoded = jsonDecode(frame.data);
    if (decoded is Map<String, dynamic>) json = decoded;
  } on FormatException {
    // Not JSON: handled below like any unreadable frame.
  }

  switch (frame.event) {
    case 'snapshot':
      final sequence = json?['sequence'];
      return sequence is int ? SnapshotMessage(sequence) : const ChangeMessage(type: 'snapshot');
    case 'resync':
      return const ResyncMessage();
    case 'TrainPositionUpdated':
      if (json != null) {
        try {
          return PositionMessage.fromJson(json);
        } on FormatException {
          // fall through to the safe reaction
        } on TypeError {
          // fall through to the safe reaction
        }
      }
  }

  final sequence = json?['sequence'];
  final journeyId = json?['journeyId'];
  return ChangeMessage(
    type: frame.event,
    sequence: sequence is int ? sequence : int.tryParse(frame.id ?? ''),
    journeyId: journeyId is String ? journeyId : null,
  );
}
