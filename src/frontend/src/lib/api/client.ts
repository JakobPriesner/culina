import createClient from 'openapi-fetch';

import type { paths } from './generated/schema';
import { conditionalRequests, csrf, expiredSessions } from './middleware';
import { ErrorCodes, offline, timedOut, toAppError } from './problem';
import { err, ok, type Result } from './result';

/**
 * The one place that talks to the backend.
 *
 * Everything cross-cutting — credentials, CSRF, conditional requests, session
 * expiry, timeouts, failure shape — lives here rather than being remembered at
 * each call site. Components import `request` and the typed `http`; nothing
 * else in the app calls `fetch`.
 */

/** Long enough for a slow phone on a train, short enough to not look frozen. */
const defaultTimeoutMs = 15_000;

/**
 * The deadline for calls that make the server talk to somebody else's.
 *
 * Everything else here is this server answering from its own database, which is
 * fast or broken. Reading a connected recipe library is neither: one request
 * becomes a handful of round trips to an instance that may be on the other side
 * of a domestic upload, and then some image work. Fifteen seconds is right for
 * a database read and simply wrong for that — it aborted whole batches of
 * recipes and reported every one of them as unreadable.
 */
const sourceTimeoutMs = 60_000;

/**
 * Where those calls live.
 *
 * Matched by path rather than passed per call, because this is a fact about the
 * API and the API layer is the one place allowed to know the API's shape. A
 * caller choosing its own deadline is a caller that will forget to.
 */
const sourcePathPrefix = '/api/v1/recipe-sources';

const http = createClient<paths>({
  // No base URL: the generated paths already carry /api/v1, and Culina is
  // always served from the same origin as its API — which is also why there is
  // no CORS policy anywhere and no Authorization header ever.
  credentials: 'include',
  fetch: withTimeout
});

http.use(csrf, conditionalRequests, expiredSessions);

export { http };

/**
 * What a generated call hands back.
 *
 * Structural rather than the library's own `FetchResponse`, whose three type
 * parameters describe the operation and cannot be inferred from a thunk. The
 * caller's data type still flows through; the error is read as a problem
 * document, which is what every failure is.
 */
interface Call<TData> {
  readonly data?: TData;
  readonly error?: unknown;
  readonly response: Response;
}

/**
 * Runs one call and turns it into a result.
 *
 * A CSRF rejection is retried exactly once, and only when the cookie has
 * actually changed in the meantime — another tab signing in is the real cause,
 * and resending the same token would only earn the same refusal.
 */
export async function request<TData>(call: () => Promise<Call<TData>>): Promise<Result<TData>> {
  const first = await attempt(call);

  if (first.ok || first.error.code !== ErrorCodes.csrfInvalid) {
    return first;
  }

  return attempt(call);
}

async function attempt<TData>(call: () => Promise<Call<TData>>): Promise<Result<TData>> {
  try {
    const { data, error, response } = await call();

    if (error !== undefined) {
      return err(toAppError(response, error));
    }

    // A 204 has no body, and the caller's type says so.
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

/**
 * Gives every request a deadline.
 *
 * Combined with the caller's own signal rather than replacing it, so a request
 * is still cancelled when the component that started it goes away.
 */
function withTimeout(input: Request): Promise<Response> {
  const deadline = AbortSignal.timeout(deadlineFor(input.url));

  return fetch(input, {
    signal: input.signal ? AbortSignal.any([input.signal, deadline]) : deadline
  });
}

function deadlineFor(url: string): number {
  try {
    return new URL(url).pathname.startsWith(sourcePathPrefix) ? sourceTimeoutMs : defaultTimeoutMs;
  } catch {
    // Not a URL this can read, which is not a reason to have no deadline.
    return defaultTimeoutMs;
  }
}
