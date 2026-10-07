import { csrfToken } from './cookies';
import { drain, type Stream, type StreamHandlers } from './eventStream';
import { clientError, ErrorCodes, offline, toAppError, type AppError } from './problem';
import { sessionExpired } from './session';

/** The header the backend checks on every unsafe cookie-authenticated request. */
const csrfHeader = 'X-Culina-CSRF';

/**
 * Starts something on the server and reads it happening.
 *
 * `watch` follows work that is already running and can be picked back up;
 * this one *is* the work. A model writing a recipe cannot be resumed — asking
 * again is a second call and a second bill — so a dropped connection ends the
 * stream and says so rather than quietly reconnecting.
 *
 * A POST, because it starts something, which brings the two things `watch` does
 * not need: the CSRF token every unsafe request carries, and a body.
 *
 * The caller closes the stream when it is told the work finished. A body that
 * ends while the stream is still open is therefore a failure, and is reported
 * as one.
 *
 * @param path The path on this origin, e.g. `/api/v1/...`.
 * @param body What to send, already serialised. `FormData` sets its own type.
 * @param handlers What to do with each event, and with the end of the stream.
 */
export function ask<TEvent>(
  path: string,
  body: BodyInit | null,
  handlers: StreamHandlers<TEvent>
): Stream {
  const control = new AbortController();

  void once(path, body, handlers, control);

  return { close: () => control.abort() };
}

async function once<TEvent>(
  path: string,
  body: BodyInit | null,
  handlers: StreamHandlers<TEvent>,
  control: AbortController
): Promise<void> {
  const opened = await start(path, body, control.signal);

  if (control.signal.aborted) {
    return;
  }

  if (opened.error) {
    handlers.failed(opened.error);

    return;
  }

  if (!opened.body) {
    return;
  }

  await drain(opened.body, handlers, control.signal, null);

  // The body ended without the caller stopping it, which means it ended by
  // itself — a tunnel that collapsed, or a server that died mid-recipe. A
  // caller that was told the work finished closes the stream on that event, so
  // reaching here is always the bad kind of ending and is reported as one.
  // Without this, a dropped connection leaves a screen writing forever.
  if (!control.signal.aborted) {
    handlers.failed(offline());
  }
}

/** Starts the work, and turns a refusal into the app's ordinary failure. */
async function start(
  path: string,
  body: BodyInit | null,
  signal: AbortSignal
): Promise<{ body?: ReadableStream<Uint8Array>; error?: AppError }> {
  const token = csrfToken();
  let response: Response;

  try {
    response = await fetch(path, {
      method: 'POST',
      // No deadline. The whole point is that this one is slow, and the app's
      // usual timeout would cut a model off mid-sentence.
      credentials: 'include',
      headers: {
        Accept: 'text/event-stream',
        // A string body is this app's JSON, and it has to say so. Without
        // this `fetch` labels it text/plain, the endpoint's JSON binding
        // never matches the route, and the answer is a 404 about an endpoint
        // that plainly exists — which reads on screen as a button that does
        // nothing at all.
        //
        // FormData is left alone on purpose: the browser writes its own
        // multipart type with the boundary in it, and a Content-Type set here
        // would replace that with one the server cannot split.
        ...(typeof body === 'string' ? { 'Content-Type': 'application/json' } : {}),
        ...(token ? { [csrfHeader]: token } : {})
      },
      body,
      signal
    });
  } catch {
    // Never reached a server, or the caller went away.
    return { error: offline() };
  }

  if (response.ok) {
    return response.body
      ? { body: response.body }
      : { error: clientError(ErrorCodes.unexpected, 'That stream could not be read.') };
  }

  if (response.status === 401) {
    // The same thing the typed client does on a 401, because this bypasses it:
    // a stream is opened with `fetch`, so its middleware never runs.
    sessionExpired();
  }

  return { error: toAppError(response, await response.json().catch(() => undefined)) };
}
