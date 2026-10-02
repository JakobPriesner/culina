import { version } from '$app/environment';
import { page } from '$app/state';
import { http, request } from '$api';

import {
  describeClient,
  describeMoment,
  learnClientHints,
  type ClientContext,
  type MomentContext
} from './clientContext';

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
 *
 * Each batch says everything the page knows about the browser and device it
 * runs on, and each record the state of the page when it happened; see
 * `clientContext.ts`.
 */

/** What the server records, and decides the level of. */
export type ReportedEvent =
  | 'uncaught_error'
  | 'unhandled_rejection'
  | 'render_failed'
  | 'csp_violation'
  | 'service_worker_failed';

interface LogRecord extends MomentContext {
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

/**
 * What the browser lets `keepalive` requests carry between them: once their
 * bodies in flight would pass 64 KiB, it fails the next one outright. Only this
 * module sends them, so it can keep the count itself.
 */
const keepaliveBudget = 64 * 1024;

/** Long enough that a burst becomes one request, short enough to arrive before the tab closes. */
const flushDelayMs = 5_000;

/** A page failing in a loop has said everything useful long before this. */
const mostPerPage = 50;

/** What the service worker sends when it fails; see `tell` in service-worker.ts. */
const workerFailed = 'culina:failed';

let queue: LogRecord[] = [];
let timer: ReturnType<typeof setTimeout> | undefined;
let keptAlive = 0;
const seen = new Set<string>();
const encoder = new TextEncoder();

/**
 * Records one thing that went wrong.
 *
 * Takes whatever was thrown — an `Error`, a string, a rejection reason that is
 * neither — because a handler cannot choose what reaches it.
 */
export function report(event: ReportedEvent, thrown: unknown): void {
  const record = { event, route: currentRoute(), ...describe(thrown), ...describeMoment() };
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
 * page is gone — but only while it fits. The browser fails a `keepalive`
 * request over its budget rather than sending it, and a failure here is
 * silent, so a batch that does not fit goes as an ordinary request instead:
 * it still arrives unless the page closes first. Batches are cut by size as
 * well as count so that each one can fit on its own. Through the typed client,
 * so a signed-in page sends its CSRF token like every other write.
 */
export function flush(): void {
  clearTimeout(timer);
  timer = undefined;

  const client = describeClient();

  while (queue.length > 0) {
    const body = { appVersion: version, client, records: takeBatch(client) };
    const size = bytes(body);
    const keepalive = keptAlive + size <= keepaliveBudget;

    if (keepalive) {
      keptAlive += size;
    }

    // Fire and forget: `request` never throws, and a failure is not worth a
    // second attempt or a word to anybody.
    void request(() => http.POST('/api/v1/log-records', { body, keepalive })).then(() => {
      if (keepalive) {
        keptAlive -= size;
      }
    });
  }
}

/**
 * The next batch off the queue: at most what the server accepts, and small
 * enough to travel with `keepalive` when nothing else is. A record too big for
 * that on its own still goes, alone.
 */
function takeBatch(client: ClientContext): LogRecord[] {
  const batch: LogRecord[] = [];
  let size = bytes({ appVersion: version, client, records: [] });

  while (queue.length > 0 && batch.length < mostPerBatch) {
    // One more for the comma between records.
    const next = bytes(queue[0]) + 1;

    if (batch.length > 0 && size + next > keepaliveBudget) {
      break;
    }

    batch.push(queue.shift()!);
    size += next;
  }

  return batch;
}

/** The size of the body as it is sent, which is UTF-8 and not string length. */
function bytes(value: unknown): number {
  return encoder.encode(JSON.stringify(value)).length;
}

/**
 * Listens for everything nobody else caught.
 *
 * Errors a component boundary or the router catch never get this far; those
 * call `report` themselves.
 */
export function startReporting(): () => void {
  learnClientHints();

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
  keptAlive = 0;
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
