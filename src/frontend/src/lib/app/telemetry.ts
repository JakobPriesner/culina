import { version } from '$app/environment';
import { page } from '$app/state';
import { http, request } from '$api';

/**
 * Tells the operator what went wrong in somebody's browser.
 *
 * The server already exports its traces, metrics and logs; without this, the
 * one place an incident could not be seen from the telemetry was the app
 * itself — a page that threw on somebody's phone left a blank screen and no
 * line anywhere. Records go to the API rather than to a collector: the
 * collector stays on the operator's network, the policy's `connect-src
 * 'self'` stays as it is, and the server exports them with everything else,
 * already carrying the request id and who was signed in.
 *
 * Deliberately small and never in the way. A report that cannot be sent is
 * dropped — never retried, never queued for later, and never itself reported,
 * which is the loop a reporter must not be able to start. Repeats are sent
 * once, and a page that is failing over and over stops after a few dozen.
 */

/** What the server records, and decides the level of. */
export type ReportedEvent =
  | 'uncaught_error'
  | 'unhandled_rejection'
  | 'render_failed'
  | 'csp_violation'
  | 'service_worker_failed';

interface LogRecord {
  readonly event: ReportedEvent;
  readonly message: string;
  readonly stack?: string;
  readonly route?: string;
}

/** The server's own ceilings; anything longer would cost the whole batch. */
const mostPerBatch = 10;
const longestMessage = 1_000;
const longestStack = 8_000;
const longestRoute = 200;

/** Long enough that a burst becomes one request, short enough to arrive before the tab closes. */
const flushDelayMs = 5_000;

/** A page failing in a loop has said everything useful long before this. */
const mostPerPage = 50;

/** What the service worker sends when it fails; see `tell` in service-worker.ts. */
const workerFailed = 'culina:failed';

let queue: LogRecord[] = [];
let timer: ReturnType<typeof setTimeout> | undefined;
const seen = new Set<string>();

/**
 * Records one thing that went wrong.
 *
 * Takes whatever was thrown — an `Error`, a string, a rejection reason that is
 * neither — because a handler cannot choose what reaches it.
 */
export function report(event: ReportedEvent, thrown: unknown): void {
  const record = { event, route: currentRoute(), ...describe(thrown) };
  const key = `${record.event} ${record.route} ${record.message}`;

  if (seen.has(key) || seen.size >= mostPerPage) {
    return;
  }

  seen.add(key);
  queue.push(record);

  if (queue.length >= mostPerBatch) {
    flush();
  } else {
    timer ??= setTimeout(flush, flushDelayMs);
  }
}

/**
 * Sends whatever is waiting.
 *
 * `keepalive`, so a batch sent as the tab is hidden still arrives after the
 * page is gone. Through the typed client, so a signed-in page sends its CSRF
 * token like every other write.
 */
export function flush(): void {
  clearTimeout(timer);
  timer = undefined;

  while (queue.length > 0) {
    const records = queue.splice(0, mostPerBatch);

    // Fire and forget: `request` never throws, and a failure is not worth a
    // second attempt or a word to anybody.
    void request(() =>
      http.POST('/api/v1/log-records', {
        body: { appVersion: version, records },
        keepalive: true
      })
    );
  }
}

/**
 * Listens for everything nobody else caught.
 *
 * Errors a component boundary or the router catch never get this far; those
 * call `report` themselves.
 */
export function startReporting(): () => void {
  const onError = (event: ErrorEvent) => report('uncaught_error', event.error ?? event.message);
  const onRejection = (event: PromiseRejectionEvent) => report('unhandled_rejection', event.reason);
  const onViolation = (event: SecurityPolicyViolationEvent) =>
    report('csp_violation', `${event.effectiveDirective} blocked ${blocked(event.blockedURI)}`);
  const onWorkerMessage = (event: MessageEvent) => {
    const data = event.data as { type?: string } | null;

    if (data?.type === workerFailed) {
      report('service_worker_failed', data);
    }
  };
  const onHidden = () => {
    if (document.visibilityState === 'hidden') {
      flush();
    }
  };

  window.addEventListener('error', onError);
  window.addEventListener('unhandledrejection', onRejection);
  document.addEventListener('securitypolicyviolation', onViolation);
  document.addEventListener('visibilitychange', onHidden);
  navigator.serviceWorker?.addEventListener('message', onWorkerMessage);

  return () => {
    window.removeEventListener('error', onError);
    window.removeEventListener('unhandledrejection', onRejection);
    document.removeEventListener('securitypolicyviolation', onViolation);
    document.removeEventListener('visibilitychange', onHidden);
    navigator.serviceWorker?.removeEventListener('message', onWorkerMessage);
  };
}

/** Forgets what was sent and drops what was not. For tests. */
export function resetReporting(): void {
  clearTimeout(timer);
  timer = undefined;
  queue = [];
  seen.clear();
}

/**
 * The route id — `/(app)/recipes/[recipeId]` — never the address, which has
 * the recipe's id in it and says nothing more about the bug.
 */
function currentRoute(): string | undefined {
  try {
    return page.route.id?.slice(0, longestRoute);
  } catch {
    // Before the router exists, which is exactly when a boot failure happens.
    return undefined;
  }
}

function describe(thrown: unknown): { message: string; stack?: string } {
  if (thrown instanceof Error) {
    return { message: cut(String(thrown), longestMessage), stack: stackOf(thrown.stack) };
  }

  // What the service worker sends: an error's two halves, since an Error does
  // not survive being posted.
  if (typeof thrown === 'object' && thrown !== null && 'message' in thrown) {
    const { message, stack } = thrown as { message?: unknown; stack?: unknown };

    return {
      message: cut(String(message), longestMessage),
      stack: typeof stack === 'string' ? stackOf(stack) : undefined
    };
  }

  return { message: cut(textOf(thrown), longestMessage) };
}

function stackOf(stack: string | undefined): string | undefined {
  return stack ? cut(stack, longestStack) : undefined;
}

function textOf(thrown: unknown): string {
  try {
    return typeof thrown === 'string' ? thrown : (JSON.stringify(thrown) ?? String(thrown));
  } catch {
    return String(thrown);
  }
}

/**
 * Keeps a blocked address to its origin. The path of something the policy
 * refused can carry an id; the origin is what tells an operator what was
 * blocked. Keywords such as `inline` and `eval` are not addresses and pass
 * through.
 */
function blocked(uri: string): string {
  try {
    const { origin, protocol } = new URL(uri);

    // A data: or blob: address has no origin of its own to name.
    return origin === 'null' ? protocol : origin;
  } catch {
    return uri || 'inline';
  }
}

function cut(text: string, limit: number): string {
  return text.length > limit ? text.slice(0, limit) : text;
}
