import { authorization, credentialsMode } from './credentials';
import { drain, type Stream, type StreamHandlers } from './eventStream';
import { apiUrl } from './instance';
import { clientError, ErrorCodes, offline, toAppError, type AppError } from './problem';
import { sessionExpired } from './session';

/**
 * POSTs to start server work and streams its events. Unlike `watch` it cannot resume (a second call is a second bill), so a dropped connection fails.
 * The caller closes the stream on its finished event; a body ending first is reported as offline.
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

  // Ended without the caller closing it: a dropped connection, or the screen would write forever.
  if (!control.signal.aborted) {
    handlers.failed(offline());
  }
}

async function start(
  path: string,
  body: BodyInit | null,
  signal: AbortSignal
): Promise<{ body?: ReadableStream<Uint8Array>; error?: AppError }> {
  let response: Response;

  try {
    response = await fetch(apiUrl(path), {
      method: 'POST',
      // No deadline: model output is slow and the usual timeout would cut it off.
      credentials: credentialsMode,
      headers: {
        Accept: 'text/event-stream',
        // A string body is JSON (else fetch sends text/plain and the route 404s); FormData must keep the browser's multipart boundary.
        ...(typeof body === 'string' ? { 'Content-Type': 'application/json' } : {}),
        ...authorization(true)
      },
      body,
      signal
    });
  } catch {
    return { error: offline() };
  }

  if (response.ok) {
    return response.body
      ? { body: response.body }
      : { error: clientError(ErrorCodes.unexpected, 'That stream could not be read.') };
  }

  if (response.status === 401) {
    // Mirrors the typed client's 401 handling, whose middleware this plain `fetch` bypasses.
    sessionExpired();
  }

  return { error: toAppError(response, await response.json().catch(() => undefined)) };
}
