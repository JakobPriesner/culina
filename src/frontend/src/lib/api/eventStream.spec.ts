import { describe, expect, it, vi } from 'vitest';

import { drain, parse } from './eventStream';

const bodyOf = (...chunks: string[]) => {
  const encoder = new TextEncoder();

  return new ReadableStream<Uint8Array>({
    start(controller) {
      chunks.forEach((chunk) => controller.enqueue(encoder.encode(chunk)));
      controller.close();
    }
  });
};

const handlers = () => ({ message: vi.fn(), failed: vi.fn() });

describe('parse', () => {
  it('reads the data and the id of one event', () => {
    expect(parse('id: 7\ndata: {"a":1}')).toEqual({ data: '{"a":1}', id: '7' });
  });

  it('joins several data lines with a newline', () => {
    expect(parse('data: one\ndata: two').data).toBe('one\ntwo');
  });

  it('skips comments, which only keep the stream open', () => {
    expect(parse(': keep-alive')).toEqual({ data: null, id: null });
  });

  it('takes a field with no colon as an empty value', () => {
    expect(parse('data')).toEqual({ data: '', id: null });
  });
});

describe('drain', () => {
  it('delivers each event and reports the last id it saw', async () => {
    const seen = handlers();

    const last = await drain(
      bodyOf('id: 1\ndata: {"n":1}\n\nid: 2\ndata: {"n":2}\n\n'),
      seen,
      new AbortController().signal,
      null
    );

    expect(seen.message.mock.calls).toEqual([[{ n: 1 }], [{ n: 2 }]]);
    expect(last).toBe('2');
  });

  it('waits for the rest of an event split across chunks', async () => {
    const seen = handlers();

    await drain(
      bodyOf('data: {"n"', ':3}\n', '\ndata: {"n":4}\n\n'),
      seen,
      new AbortController().signal,
      null
    );

    expect(seen.message.mock.calls).toEqual([[{ n: 3 }], [{ n: 4 }]]);
  });

  it('keeps the id it was given when the body carries none', async () => {
    const last = await drain(
      bodyOf('data: {}\n\n'),
      handlers(),
      new AbortController().signal,
      'abc'
    );

    expect(last).toBe('abc');
  });

  it('does not deliver half an event when the body ends', async () => {
    const seen = handlers();

    await drain(bodyOf('data: {"n":1}'), seen, new AbortController().signal, null);

    expect(seen.message).not.toHaveBeenCalled();
  });

  it('reads nothing once it has been aborted', async () => {
    const seen = handlers();
    const control = new AbortController();

    control.abort();
    await drain(bodyOf('data: {}\n\n'), seen, control.signal, null);

    expect(seen.message).not.toHaveBeenCalled();
  });
});
