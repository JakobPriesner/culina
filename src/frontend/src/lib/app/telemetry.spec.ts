import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { flush, report, resetReporting, startReporting } from './telemetry';

/* A reporter can hurt by sending too much or by making things worse when it cannot send; neither is visible from the page. */

let sent: Request[] = [];

beforeEach(() => {
  vi.useFakeTimers();
  sent = [];
  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request) => {
      sent.push(input);

      return Promise.resolve(new Response(null, { status: 202 }));
    })
  );
});

afterEach(() => {
  resetReporting();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

async function bodies() {
  return Promise.all(
    sent.map((request) => request.clone().json() as Promise<{ records: { event: string }[] }>)
  );
}

describe('batching', () => {
  it('sends a burst as one request, a few seconds later', async () => {
    report('uncaught_error', new Error('one'));
    report('uncaught_error', new Error('two'));

    expect(sent).toHaveLength(0);

    await vi.advanceTimersByTimeAsync(5_000);

    expect(sent).toHaveLength(1);
    expect((await bodies())[0]?.records).toHaveLength(2);
  });

  it('never sends more than the server accepts in one request', async () => {
    for (let index = 0; index < 12; index++) {
      report('uncaught_error', new Error(`failure ${index}`));
    }

    flush();
    await vi.runAllTimersAsync();

    const counts = (await bodies()).map((body) => body.records.length);

    expect(counts).toEqual([10, 2]);
  });

  it('survives the page being hidden', async () => {
    report('uncaught_error', new Error('closing'));

    flush();
    await vi.runAllTimersAsync();

    expect(sent[0]?.keepalive).toBe(true);
  });

  /* The browser fails keepalive requests outright past 64 KiB in flight, without telling the reporter. */
  it('keeps within what the browser lets outlive the page, and sends the rest anyway', async () => {
    reportLargeFailures(10);

    flush();
    await vi.runAllTimersAsync();

    const sizes = await Promise.all(sent.map(sizeOf));
    const keptAlive = sizes.filter((_, index) => sent[index]?.keepalive);
    const total = (await bodies()).reduce((sum, body) => sum + body.records.length, 0);

    expect(sent.length).toBeGreaterThan(1);
    expect(sizes.every((size) => size <= keepaliveBudget)).toBe(true);
    expect(keptAlive.reduce((sum, size) => sum + size, 0)).toBeLessThanOrEqual(keepaliveBudget);
    expect(sent.some((request) => !request.keepalive)).toBe(true);
    expect(total).toBe(10);
  });

  it('keeps a request alive again once the earlier ones have arrived', async () => {
    reportLargeFailures(10);
    flush();
    await vi.runAllTimersAsync();

    reportLargeFailures(1, 10);
    flush();
    await vi.runAllTimersAsync();

    expect(sent.at(-1)?.keepalive).toBe(true);
  });
});

const keepaliveBudget = 64 * 1024;

/** As big as the server allows a record to be, each different. */
function reportLargeFailures(count: number, from = 0) {
  for (let index = from; index < from + count; index++) {
    const error = new Error(`${index} ${'x'.repeat(1_000)}`);
    error.stack = 'y'.repeat(8_000);

    report('uncaught_error', error);
  }
}

async function sizeOf(request: Request) {
  return new TextEncoder().encode(await request.clone().text()).length;
}

describe('context', () => {
  it('says where it ran once per batch, and when each record happened', async () => {
    report('uncaught_error', new Error('one'));
    report('uncaught_error', new Error('two'));

    flush();
    await vi.runAllTimersAsync();

    const body = (await sent[0]!.clone().json()) as {
      client: { sessionId?: string; viewportWidth?: number; languages?: string[] };
      records: { occurredAt?: string; online?: boolean }[];
    };

    expect(body.client).toEqual(
      expect.objectContaining({
        sessionId: expect.any(String),
        viewportWidth: window.innerWidth,
        languages: [...navigator.languages]
      })
    );
    expect(body.records).toEqual([
      expect.objectContaining({ occurredAt: expect.any(String), online: navigator.onLine }),
      expect.objectContaining({ occurredAt: expect.any(String), online: navigator.onLine })
    ]);
  });
});

describe('restraint', () => {
  it('sends the same failure once', async () => {
    report('render_failed', new Error('same'));
    report('render_failed', new Error('same'));

    flush();
    await vi.runAllTimersAsync();

    expect((await bodies())[0]?.records).toHaveLength(1);
  });

  it('stops after a few dozen, however long a page keeps failing', async () => {
    for (let index = 0; index < 200; index++) {
      report('uncaught_error', new Error(`loop ${index}`));
    }

    flush();
    await vi.runAllTimersAsync();

    const total = (await bodies()).reduce((sum, body) => sum + body.records.length, 0);

    expect(total).toBe(50);
  });

  it('keeps every field under the server ceiling', async () => {
    const error = new Error('x'.repeat(5_000));
    error.stack = 'y'.repeat(20_000);

    report('uncaught_error', error);
    flush();
    await vi.runAllTimersAsync();

    const [record] = (await sent[0]!.clone().json()).records as {
      message: string;
      stack: string;
    }[];

    expect(record!.message.length).toBeLessThanOrEqual(1_000);
    expect(record!.stack.length).toBeLessThanOrEqual(8_000);
  });

  it('drops a report it could not send, without throwing or trying again', async () => {
    const offline = vi.fn(() => Promise.reject(new TypeError('Failed to fetch')));

    vi.stubGlobal('fetch', offline);

    report('uncaught_error', new Error('offline'));

    expect(() => flush()).not.toThrow();
    await vi.runAllTimersAsync();

    expect(offline).toHaveBeenCalledTimes(1);
  });
});

describe('listening', () => {
  it('reports what nobody caught, and stops when asked', async () => {
    const stop = startReporting();
    // No `error` on it: the test runner treats a real one as an uncaught exception.
    const uncaught = (message: string) => Object.assign(new Event('error'), { message });

    window.dispatchEvent(uncaught('nobody caught me'));
    stop();
    window.dispatchEvent(uncaught('after stopping'));

    flush();
    await vi.runAllTimersAsync();

    const records = (await bodies())[0]?.records as { event: string; message: string }[];

    expect(records).toEqual([
      expect.objectContaining({ event: 'uncaught_error', message: 'nobody caught me' })
    ]);
  });

  it('names what the policy blocked by its origin, never its path', async () => {
    const stop = startReporting();
    const violation = new Event('securitypolicyviolation') as SecurityPolicyViolationEvent;

    Object.assign(violation, {
      effectiveDirective: 'img-src',
      blockedURI: 'https://images.example/recipes/0198c0de-1111-7000-8000-000000000000.jpg'
    });
    document.dispatchEvent(violation);
    stop();

    flush();
    await vi.runAllTimersAsync();

    const records = (await bodies())[0]?.records as { event: string; message: string }[];

    expect(records).toEqual([
      expect.objectContaining({
        event: 'csp_violation',
        message: 'img-src blocked https://images.example'
      })
    ]);
  });
});

describe('whose problem it is', () => {
  const ours = `${location.origin}/_app/immutable/chunks/abc.js`;
  const theirs = 'chrome-extension://abcdef/content.js';

  async function recorded(dispatch: () => void) {
    const stop = startReporting();

    dispatch();
    stop();
    flush();
    await vi.runAllTimersAsync();

    return (await bodies()).flatMap((body) => body.records);
  }

  const failure = (init: { message?: string; filename?: string; stack?: string }) => () => {
    // No `error` on it: the test runner treats a real one as an uncaught exception.
    const error = init.stack ? { stack: init.stack } : undefined;

    window.dispatchEvent(Object.assign(new Event('error'), { message: 'boom', ...init, error }));
  };

  const violation = (init: object) => () =>
    document.dispatchEvent(
      Object.assign(new Event('securitypolicyviolation'), {
        effectiveDirective: 'script-src',
        ...init
      })
    );

  it('drops an error from an extension script', async () => {
    expect(await recorded(failure({ filename: theirs }))).toEqual([]);
  });

  it('drops an error from another origin', async () => {
    expect(await recorded(failure({ filename: 'https://cdn.example/lib.js' }))).toEqual([]);
  });

  it('drops the opaque error of a cross-origin script', async () => {
    expect(await recorded(failure({ message: 'Script error.', filename: '' }))).toEqual([]);
  });

  it('keeps an error from our own script', async () => {
    expect(await recorded(failure({ filename: ours }))).toHaveLength(1);
  });

  it('keeps an error with no file but a stack of ours', async () => {
    expect(await recorded(failure({ stack: `Error: boom\n    at f (${ours}:1:2)` }))).toHaveLength(
      1
    );
  });

  it('drops an error whose stack is only an extension', async () => {
    expect(await recorded(failure({ stack: `Error: boom\n    at f (${theirs}:1:2)` }))).toEqual([]);
  });

  it('keeps an error whose stack has one of our frames among theirs', async () => {
    const stack = `Error: boom\n    at g (${theirs}:1:2)\n    at f (${ours}:3:4)`;

    expect(await recorded(failure({ stack }))).toHaveLength(1);
  });

  it('drops a violation caused by an extension', async () => {
    expect(await recorded(violation({ sourceFile: theirs, blockedURI: 'inline' }))).toEqual([]);
    expect(await recorded(violation({ blockedURI: 'moz-extension://abcdef/inject.js' }))).toEqual(
      []
    );
  });

  it('keeps a violation from our own page', async () => {
    expect(await recorded(violation({ sourceFile: '', blockedURI: 'eval' }))).toHaveLength(1);
  });
});
