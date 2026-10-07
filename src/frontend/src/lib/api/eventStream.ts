/** The reading half of a server-sent event stream; connecting and retrying live in `events.ts`. */

import type { AppError } from './problem';

export interface Stream {
  /** Stops reading; never stops the work on the other end. */
  close(): void;
}

export interface StreamHandlers<TEvent> {
  message: (event: TEvent) => void;

  /** The stream is over for good; carries why (lapsed session, missing run, network down). */
  failed: (error: AppError) => void;
}

/** Reads events until the body ends and returns the last id. An ended body is not a failure; the caller decides whether to resume. */
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

      // Events end at a blank line; the remainder is a partial event.
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

export function parse(block: string): { data: string | null; id: string | null } {
  const data: string[] = [];
  let id: string | null = null;

  for (const line of block.split(/\r?\n/)) {
    // A leading colon is a keep-alive comment.
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
