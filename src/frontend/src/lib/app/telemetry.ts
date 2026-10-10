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
 * Reports browser errors to the API (not a collector) so the server exports them with the request id and user.
 * A failed report is dropped, never retried or itself reported; repeats go once, and a failing page stops after a few dozen.
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

/** Browsers fail the next `keepalive` request once bodies in flight pass 64 KiB; only this module sends them, so it counts. */
const keepaliveBudget = 64 * 1024;

/** Long enough that a burst becomes one request, short enough to arrive before the tab closes. */
const flushDelayMs = 5_000;

const mostPerPage = 50;

/** What the service worker sends when it fails; see `tell` in service-worker.ts. */
const workerFailed = 'culina:failed';

let queue: LogRecord[] = [];
let timer: ReturnType<typeof setTimeout> | undefined;
let keptAlive = 0;
const seen = new Set<string>();
const encoder = new TextEncoder();

/** Records one thing that went wrong; takes anything thrown, since a handler cannot choose what reaches it. */
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

/** Sends what is waiting. `keepalive` survives page close but fails silently over its budget, so a batch that does not fit goes as an ordinary request. */
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

    // Fire and forget: `request` never throws.
    void request(() => http.POST('/api/v1/log-records', { body, keepalive })).then(() => {
      if (keepalive) {
        keptAlive -= size;
      }
    });
  }
}

/** The next batch: at most what the server accepts and small enough for `keepalive`; an oversized record goes alone. */
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

function bytes(value: unknown): number {
  return encoder.encode(JSON.stringify(value)).length;
}

/** Listens for errors nobody else caught; component boundaries and the router call `report` themselves. */
export function startReporting(): () => void {
  learnClientHints();

  const onError = (event: ErrorEvent) => {
    const stack: unknown = event.error?.stack;

    // A cross-origin script's error arrives with nothing to tell where it came from.
    if (isOurs(event.filename, stack) && (event.message !== 'Script error.' || stack)) {
      report('uncaught_error', event.error ?? event.message);
    }
  };
  const onRejection = (event: PromiseRejectionEvent) => {
    if (isOurs('', event.reason?.stack)) {
      report('unhandled_rejection', event.reason);
    }
  };
  const onViolation = (event: SecurityPolicyViolationEvent) => {
    if (!fromExtension(event.sourceFile) && !fromExtension(event.blockedURI)) {
      report('csp_violation', `${event.effectiveDirective} blocked ${blocked(event.blockedURI)}`);
    }
  };
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

export function resetReporting(): void {
  clearTimeout(timer);
  timer = undefined;
  keptAlive = 0;
  queue = [];
  seen.clear();
}

const extensionScheme = /^(?:chrome|moz|safari|safari-web)-extension:/;

/** Extensions inject scripts and load images into the page; a violation they cause is theirs to fix. */
function fromExtension(uri: string | undefined): boolean {
  return extensionScheme.test(uri ?? '');
}

/**
 * Whether the code that failed is ours. A file names it outright; without one, any frame of the stack
 * in our origin will do. Minified frames are still our `/_app/immutable/` addresses, so no source map is needed.
 * With neither a file nor addresses in the stack there is nothing to rule it out.
 */
function isOurs(filename: string, stack: unknown): boolean {
  if (filename) {
    return filename.startsWith(`${location.origin}/`);
  }

  const frames =
    (typeof stack === 'string' ? stack : '').match(/\b[a-z][a-z0-9+.-]*:\/\/[^\s)]+/gi) ?? [];

  return frames.length === 0 || frames.some((frame) => frame.startsWith(`${location.origin}/`));
}

/** The route id (`/(app)/recipes/[recipeId]`), never the address, which carries the recipe's id. */
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

/** Keeps a blocked address to its origin, since a path can carry an id; keywords such as `inline` and `eval` pass through. */
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
