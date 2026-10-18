import 'dart:async';
import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:smartrail_core/smartrail_core.dart';

Stream<List<int>> bytesOf(List<String> chunks) => Stream.fromIterable([for (final c in chunks) utf8.encode(c)]);

Future<List<SseFrame>> parse(List<String> chunks) => parseSse(bytesOf(chunks)).toList();

void main() {
  test('reads id, event and data of a frame', () async {
    final frames = await parse(['id: 1043\nevent: DelayChanged\ndata: {"a":1}\n\n']);

    expect(frames, hasLength(1));
    expect(frames.single.id, '1043');
    expect(frames.single.event, 'DelayChanged');
    expect(frames.single.data, '{"a":1}');
  });

  test('reads several frames, and CRLF line ends', () async {
    final frames = await parse(['event: a\r\ndata: 1\r\n\r\nevent: b\r\ndata: 2\r\n\r\n']);

    expect(frames.map((f) => f.event), ['a', 'b']);
    expect(frames.map((f) => f.data), ['1', '2']);
  });

  test('a frame split across chunks, even in the middle of a line, is read whole', () async {
    final frames = await parse(['id: 7\nev', 'ent: StopStatusCh', 'anged\nda', 'ta: {"x":', '1}\n', '\n']);

    expect(frames.single.id, '7');
    expect(frames.single.event, 'StopStatusChanged');
    expect(frames.single.data, '{"x":1}');
  });

  test('a multi-byte character split across chunks survives', () async {
    final bytes = utf8.encode('event: m\ndata: Colombo – Kandy\n\n');
    final dash = bytes.indexOf(0xE2); // the first byte of the en dash
    final frames = await parseSse(Stream.fromIterable([bytes.sublist(0, dash + 1), bytes.sublist(dash + 1)])).toList();

    expect(frames.single.data, 'Colombo – Kandy');
  });

  test('comment lines (the heartbeat) are skipped and make no frame', () async {
    final frames = await parse([': keep-alive\n\n: keep-alive\n\nevent: a\ndata: 1\n\n']);

    expect(frames, hasLength(1));
    expect(frames.single.event, 'a');
  });

  test('several data lines are joined with a newline', () async {
    final frames = await parse(['data: one\ndata: two\n\n']);

    expect(frames.single.data, 'one\ntwo');
    expect(frames.single.event, 'message');
  });

  test('a frame without data is skipped, and fields do not leak into the next frame', () async {
    final frames = await parse(['id: 5\nevent: lonely\n\ndata: next\n\n']);

    expect(frames, hasLength(1));
    expect(frames.single.id, isNull);
    expect(frames.single.event, 'message');
  });

  test('a frame that is not finished by a blank line when the stream ends is dropped', () async {
    final frames = await parse(['event: a\ndata: 1\n']);

    expect(frames, isEmpty);
  });

  test('no space after the colon is fine, and only one space is removed', () async {
    final frames = await parse(['data:x\n\ndata:  y\n\n']);

    expect(frames.map((f) => f.data), ['x', ' y']);
  });

  test('unknown fields and retry: are ignored', () async {
    final frames = await parse(['retry: 3000\nfoo: bar\ndata: 1\n\n']);

    expect(frames.single.data, '1');
  });
}
