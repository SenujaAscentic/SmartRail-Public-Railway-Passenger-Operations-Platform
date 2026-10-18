import 'dart:async';
import 'dart:convert';

/// One frame of a Server-Sent Events stream: `id:`, `event:` and `data:` lines
/// ended by a blank line.
class SseFrame {
  const SseFrame({this.id, required this.event, required this.data});

  /// The `id:` line (the stream sequence for live events); null when absent.
  final String? id;

  /// The `event:` line; "message" when the frame has none.
  final String event;

  /// The `data:` lines joined with a newline.
  final String data;
}

/// Reads frames from the bytes of an SSE response (ADR-02: SSE is plain HTTP,
/// so about 30 lines are enough and no package is needed).
/// Comment lines (`: keep-alive`, the heartbeat) are skipped; a frame with no
/// `data:` line is skipped too.
///
/// Built from stream transformers, not an `async*` function, so that cancelling
/// the subscription reaches the connection at once (a screen that closes must
/// not leave the connection or its timers behind).
Stream<SseFrame> parseSse(Stream<List<int>> bytes) {
  String? id;
  var event = 'message';
  final data = <String>[];

  final frames = StreamTransformer<String, SseFrame>.fromHandlers(
    handleData: (line, sink) {
      if (line.isEmpty) {
        if (data.isNotEmpty) sink.add(SseFrame(id: id, event: event, data: data.join('\n')));
        id = null;
        event = 'message';
        data.clear();
        return;
      }
      if (line.startsWith(':')) return;

      final colon = line.indexOf(':');
      final field = colon < 0 ? line : line.substring(0, colon);
      var value = colon < 0 ? '' : line.substring(colon + 1);
      if (value.startsWith(' ')) value = value.substring(1);

      switch (field) {
        case 'id':
          id = value;
        case 'event':
          event = value;
        case 'data':
          data.add(value);
        // "retry:" and unknown fields are ignored.
      }
    },
  );

  // LineSplitter accepts "\n", "\r\n" and "\r" and copes with a line that
  // arrives in several chunks.
  return bytes.transform(utf8.decoder).transform(const LineSplitter()).transform(frames);
}
