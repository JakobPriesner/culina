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
 * of a domestic upload. Fifteen seconds is right for a database read and simply
 * wrong for that — it aborted whole pages of somebody's library and reported
 * every recipe in them as unreadable.
 *
 * Bringing the recipes over is no longer one of these calls: that request only
 * starts an import and the recipes arrive over a stream, which has no deadline
 * because it is not a request that is waiting for an answer.
 */
const patientTimeoutMs = 60_000;

/**
 * Where those calls live.
 *
 * Matched by path rather than passed per call, because this is a fact about the
 * API and the API layer is the one place allowed to know the API's shape. A
 * caller choosing its own deadline is a caller that will forget to.
 *
 * The assistant belongs here as plainly as importing does, and was missing:
 * drawing a picture is fifteen to forty seconds of somebody else's machine
 * doing the slowest thing this app asks of anyone. At the default deadline the
 * browser gave up first — and the server, which knew nothing of that, finished
 * the drawing, paid for it and stored it. The screen said it had failed while
 * the picture sat on disk.
 */
const patientPaths = [
  '/api/v1/recipe-sources',
  // Improving, drafting and reading a photograph: a model writing a whole
  // recipe, not a database read.
  '/api/v1/recipe-drafts',
  // Asking every connected provider what it offers, one after another.
  '/api/v1/settings/assistance/models'
];

/**
 * The deadline for asking a model to draw.
 *
 * Longer again, because drawing is not composing with a picture at the end of
 * it: it is the one call where a provider spends real time on a machine of its
 * own. Sixteen seconds is a fast one, and a detailed prompt on a busy afternoon
 * is minutes. Ten seconds past the server's own two minutes, so the server is
 * always the one to give up first and say why.
 */
const drawingTimeoutMs = 130_000;

/** Drawing, which is a POST to a recipe's own picture. */
const drawingPath = /^\/api\/v1\/recipes\/[^/]+\/image$/;

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
  const deadline = AbortSignal.timeout(deadlineFor(input.url, input.method));

  return fetch(input, {
    signal: input.signal ? AbortSignal.any([input.signal, deadline]) : deadline
  });
}

function deadlineFor(url: string, method: string): number {
  try {
    const { pathname } = new URL(url);

    // A POST to a recipe's picture asks a model to draw one; a PUT sends bytes
    // that are already here. Same address, three different waits.
    if (method === 'POST' && drawingPath.test(pathname)) {
      return drawingTimeoutMs;
    }

    return patientPaths.some((path) => pathname.startsWith(path))
      ? patientTimeoutMs
      : defaultTimeoutMs;
  } catch {
    // Not a URL this can read, which is not a reason to have no deadline.
    return defaultTimeoutMs;
  }
}
