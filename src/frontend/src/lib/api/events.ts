import { drain, type Stream, type StreamHandlers } from './eventStream';
import { clientError, ErrorCodes, offline, toAppError, type AppError } from './problem';

/**
 * Reads streams from the backend by hand: OpenAPI can't describe them, and `EventSource` reports every
 * failure as one bare `error` and can't send `Last-Event-ID`, so no real status, app error shape or resume.
 */

export { ask } from './ask';
export type { Stream, StreamHandlers };

/** Reconnect attempts before giving up; each resumes from the last event, so retrying repeats nothing. */
const attemptsAllowed = 5;

/** Long enough to be past a blip, short enough that nobody reads it as broken. */
const backoffMs = [500, 1000, 2000, 4000, 8000];

/** Reads a server-sent event stream from `path`, resuming after event id `from` if given. */
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

async function open(
  path: string,
  from: string | null,
  signal: AbortSignal
): Promise<{ body?: ReadableStream<Uint8Array>; error?: AppError }> {
  let response: Response;

  try {
    response = await fetch(path, {
      // No deadline: a stream stays open; the usual timeout would cut it every 15s.
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

function pause(ms: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve) => {
    const finish = () => {
      clearTimeout(timer);
      signal.removeEventListener('abort', finish);
      resolve();
    };
    const timer = setTimeout(finish, ms);

    signal.addEventListener('abort', finish, { once: true });
  });
}
