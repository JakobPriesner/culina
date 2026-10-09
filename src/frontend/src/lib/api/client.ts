import createClient from 'openapi-fetch';

import type { paths } from './generated/schema';
import { csrfToken } from './cookies';
import { conditionalRequests, csrf, expiredSessions } from './middleware';
import { ErrorCodes, offline, timedOut, toAppError } from './problem';
import { err, ok, type Result } from './result';

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
  // No base URL: paths carry /api/v1 and the API is same-origin (hence no CORS, no Authorization header).
  credentials: 'include',
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
function withTimeout(input: Request): Promise<Response> {
  const deadline = AbortSignal.timeout(deadlineFor(input.url));

  return fetch(input, {
    signal: input.signal ? AbortSignal.any([input.signal, deadline]) : deadline
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
