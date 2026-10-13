import createClient from 'openapi-fetch';

import type { paths } from './generated/schema';
import { csrfToken } from './cookies';
import { authorization, credentialsMode } from './credentials';
import { apiUrl, instanceOrigin, loadInstance } from './instance';
import { conditionalRequests, csrf, expiredSessions } from './middleware';
import { ErrorCodes, offline, timedOut, toAppError } from './problem';
import { native } from './platform';
import { err, ok, type Result } from './result';
import { send } from './transport';

/** The one place that talks to the backend; nothing else in the app calls `fetch`. */

/** Long enough for a slow phone on a train, short enough to not look frozen. */
const defaultTimeoutMs = 15_000;

/**
 * Deadline for calls that make the server talk to somebody else's (connected recipe libraries):
 * 15s aborted whole pages there. Imports stream, so they need none.
 */
const patientTimeoutMs = 60_000;

/**
 * Matched by path so callers can't forget. Streaming calls (assistant recipes, drawing) go through
 * `ask` in `./events`: a deadline would abort a response meant to stay open.
 */
const patientPaths = [
  '/api/v1/recipe-sources',
  // Queries every connected provider in turn.
  '/api/v1/settings/assistance/models'
];

/**
 * Restoring an archive uploads the largest body the app sends (photos inline), then the server writes
 * every recipe before it answers; a home upload link alone can outlast 60s.
 */
const restoreTimeoutMs = 600_000;

const restorePath = /^\/api\/v1\/households\/[^/]+\/archive$/;

const http = createClient<paths>({
  // No base URL: paths carry /api/v1. The website's API is same-origin; the app's server is chosen at run time,
  // so its requests are moved there as they are sent (`toInstance`).
  credentials: credentialsMode,
  fetch: withTimeout
});

http.use(csrf, conditionalRequests, expiredSessions);

export { http };

/** Structural stand-in for openapi-fetch's `FetchResponse`, whose type parameters can't be inferred from a thunk. */
interface Call<TData> {
  readonly data?: TData;
  readonly error?: unknown;
  readonly response: Response;
}

/**
 * Runs one call into a result. A CSRF rejection is retried once, only if the cookie changed
 * meanwhile (another tab signed in); resending the same token would fail again.
 */
export async function request<TData>(call: () => Promise<Call<TData>>): Promise<Result<TData>> {
  const tokenSent = csrfToken();
  const first = await attempt(call);

  if (first.ok || first.error.code !== ErrorCodes.csrfInvalid || csrfToken() === tokenSent) {
    return first;
  }

  return attempt(call);
}

async function attempt<TData>(call: () => Promise<Call<TData>>): Promise<Result<TData>> {
  try {
    const { data, error, response } = await call();

    // openapi-fetch gives `error: undefined` for an empty body, so an empty proxy 403 would pass as success.
    if (error !== undefined || !response.ok) {
      return err(toAppError(response, error));
    }

    return ok(data as TData);
  } catch (thrown) {
    return err(describe(thrown));
  }
}

/** Distinguishes "we gave up" from "the network did", because the wording differs. */
function describe(thrown: unknown) {
  if (thrown instanceof DOMException && thrown.name === 'TimeoutError') {
    return timedOut();
  }

  return offline();
}

/** Gives every request a deadline, combined with the caller's signal so unmounting still cancels. */
async function withTimeout(input: Request): Promise<Response> {
  const deadline = AbortSignal.timeout(deadlineFor(input.url));
  const signal = input.signal ? AbortSignal.any([input.signal, deadline]) : deadline;

  return native ? send(await toInstance(input, signal)) : fetch(input, { signal });
}

/**
 * The same request, addressed to the connected server instead of the app's own origin. The body is read whole: WebKit
 * cannot send a streamed request body, and these are JSON or a form of photos.
 */
async function toInstance(request: Request, signal: AbortSignal): Promise<Request> {
  await loadInstance();

  // Not connected yet: nothing to ask, and the app's own origin would answer every path with the shell. Reads as offline.
  if (!instanceOrigin()) {
    throw new TypeError('No server is connected.');
  }

  const { pathname, search } = new URL(request.url);
  const hasBody = request.method !== 'GET' && request.method !== 'HEAD';

  // The bearer token, once more, now that it has been read: the middleware runs first, and on a cold start the
  // session load runs beside the root layout's, before the token is out of the Keychain. Without it that first
  // /users/me went out bare, answered 401, and the 401 deleted the token: every launch signed the person out.
  const headers = new Headers(request.headers);

  for (const [name, value] of Object.entries(authorization(true))) {
    if (!headers.has(name)) {
      headers.set(name, value);
    }
  }

  return new Request(apiUrl(`${pathname}${search}`), {
    method: request.method,
    headers,
    body: hasBody ? await request.blob() : null,
    credentials: request.credentials,
    signal
  });
}

function deadlineFor(url: string): number {
  try {
    const { pathname } = new URL(url);

    if (restorePath.test(pathname)) {
      return restoreTimeoutMs;
    }

    return patientPaths.some((path) => pathname.startsWith(path))
      ? patientTimeoutMs
      : defaultTimeoutMs;
  } catch {
    // Unreadable URL still gets a deadline.
    return defaultTimeoutMs;
  }
}
