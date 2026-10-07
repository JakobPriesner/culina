/**
 * The reading half of a server-sent event stream: bytes in, decoded events out.
 * Opening the connection, retrying and the session are `events.ts`.
 */

import type { AppError } from './problem';

/** A stream that is being read. */
export interface Stream {
  /** Stops reading. Never stops the work on the other end. */
  close(): void;
}

/** What a stream tells its caller. */
export interface StreamHandlers<TEvent> {
  /** One decoded event. */
  message: (event: TEvent) => void;

  /**
   * The stream is over and will not come back by itself.
   *
   * Carries why, so the screen can say it: a session that lapsed, a run that is
   * no longer there, or a network that stayed down across every retry.
   */
  failed: (error: AppError) => void;
}

/**
 * Reads events until the body ends, and says which id it got to.
 *
 * A body that ends is not by itself a failure — a proxy cutting an idle
 * connection looks exactly like a server that finished — so this returns and
 * lets the caller decide whether to pick it back up.
 */
export async function drain<TEvent>(
  body: ReadableStream<Uint8Array>,
  handlers: StreamHandlers<TEvent>,
  signal: AbortSignal,
  from: string | null
): Promise<string | null> {
  const reader = body.getReader();
  const decoder = new TextDecoder();

  let pending = '';
  let last = from;

  try {
    while (!signal.aborted) {
      const { done, value } = await reader.read();

      if (done) {
        break;
      }

      pending += decoder.decode(value, { stream: true });

      // Events are separated by a blank line; anything after the last one is
      // half an event, and waits for the rest of it.
      const blocks = pending.split(/\r?\n\r?\n/);

      pending = blocks.pop() ?? '';

      for (const block of blocks) {
        const event = parse(block);

        if (event.id !== null) {
          last = event.id;
        }

        if (event.data !== null) {
          handlers.message(JSON.parse(event.data) as TEvent);
        }
      }
    }
  } catch {
    // A broken connection, which the caller retries.
  } finally {
    reader.cancel().catch(() => undefined);
  }

  return last;
}

/** One event block, as the format defines it: fields, in any order. */
export function parse(block: string): { data: string | null; id: string | null } {
  const data: string[] = [];
  let id: string | null = null;

  for (const line of block.split(/\r?\n/)) {
    // A line beginning with a colon is a comment, which is how a stream says
    // nothing out loud to keep itself open.
    if (line.startsWith(':')) {
      continue;
    }

    const colon = line.indexOf(':');
    const field = colon === -1 ? line : line.slice(0, colon);
    const value = colon === -1 ? '' : line.slice(colon + 1).replace(/^ /, '');

    if (field === 'data') {
      data.push(value);
    } else if (field === 'id') {
      id = value;
    }
  }

  return { data: data.length > 0 ? data.join('\n') : null, id };
}
