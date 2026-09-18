import { clientError, ErrorCodes, offline, toAppError, type AppError } from './problem';

/**
 * The one place that reads a stream from the backend.
 *
 * Here beside the typed client for the same reason `fetch` is: a caller that
 * opened its own stream would also have to remember that the cookie is what
 * authenticates it, what a failure looks like, and how to pick the stream back
 * up. The generated client cannot do this — OpenAPI describes requests that
 * end — so this is small, typed by the caller, and deliberate.
 *
 * `fetch` rather than `EventSource`, which is the obvious tool and the wrong
 * one. `EventSource` reports every failure as one bare `error` event: a session
 * that expired, a run the server has forgotten and a tunnel that collapsed are
 * indistinguishable, which leaves the only honest thing to say on screen
 * "something stopped". It also cannot send a header, so it cannot ask to resume
 * from where it left off. Reading the body by hand costs the parser below and
 * buys a real status code, the app's ordinary failure shape, and a reconnect
 * that does not repeat itself.
 */

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
 * How many times a dropped connection is picked back up before giving up.
 *
 * A stream that is minutes long will meet a sleeping laptop, a switched
 * network, or a proxy with opinions. Each retry resumes from the last event
 * that arrived, so retrying costs nothing and repeats nothing.
 */
const attemptsAllowed = 5;

/** Long enough to be past a blip, short enough that nobody reads it as broken. */
const backoffMs = [500, 1000, 2000, 4000, 8000];

/**
 * Reads a server-sent event stream.
 *
 * @param path The path on this origin, e.g. `/api/v1/...`.
 * @param handlers What to do with each event, and with the end of the stream.
 * @param from The last event id this caller already has, to resume from.
 */
export function watch<TEvent>(
  path: string,
  handlers: StreamHandlers<TEvent>,
  from: string | null = null
): Stream {
  const control = new AbortController();

  void follow(path, handlers, control, from);

  return { close: () => control.abort() };
}

async function follow<TEvent>(
  path: string,
  handlers: StreamHandlers<TEvent>,
  control: AbortController,
  from: string | null
): Promise<void> {
  let last = from;
  let attempts = 0;

  while (!control.signal.aborted) {
    const opened = await open(path, last, control.signal);

    if (control.signal.aborted) {
      return;
    }

    if (opened.error) {
      // An answer, and a refusal: retrying would earn the same one.
      handlers.failed(opened.error);

      return;
    }

    if (opened.body) {
      attempts = 0;

      last = await drain(opened.body, handlers, control.signal, last);

      if (control.signal.aborted) {
        return;
      }
    }

    attempts += 1;

    if (attempts > attemptsAllowed) {
      handlers.failed(offline());

      return;
    }

    await pause(backoffMs[attempts - 1] ?? 8000, control.signal);
  }
}

/** Asks for the stream, from where this caller left off. */
async function open(
  path: string,
  from: string | null,
  signal: AbortSignal
): Promise<{ body?: ReadableStream<Uint8Array>; error?: AppError }> {
  let response: Response;

  try {
    response = await fetch(path, {
      // No deadline: a stream is meant to stay open, and the app's usual
      // timeout would cut it off every fifteen seconds.
      credentials: 'include',
      headers: {
        Accept: 'text/event-stream',
        ...(from ? { 'Last-Event-ID': from } : {})
      },
      signal
    });
  } catch {
    // Never reached a server: worth retrying, unlike anything with a status.
    return {};
  }

  if (!response.ok) {
    const body = await response.json().catch(() => undefined);

    return { error: toAppError(response, body) };
  }

  if (!response.body) {
    return {
      error: clientError(ErrorCodes.unexpected, 'That stream could not be read.')
    };
  }

  return { body: response.body };
}

/**
 * Reads events until the body ends, and says which id it got to.
 *
 * A body that ends is not by itself a failure — a proxy cutting an idle
 * connection looks exactly like a server that finished — so this returns and
 * lets the caller decide whether to pick it back up.
 */
async function drain<TEvent>(
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
function parse(block: string): { data: string | null; id: string | null } {
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

function pause(ms: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve) => {
    const timer = setTimeout(resolve, ms);

    signal.addEventListener('abort', () => {
      clearTimeout(timer);
      resolve();
    });
  });
}
