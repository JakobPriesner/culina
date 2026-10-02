import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { flush, report, resetReporting, startReporting } from './telemetry';

/*
 * A reporter has two ways to hurt: sending too much, and making things worse
 * when it cannot send. Both are invisible from the page, so both are asserted.
 */

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

  /*
   * The browser fails a keepalive request outright once the keepalive bodies in
   * flight pass 64 KiB, and the reporter never hears of it — so a page with a
   * lot to say would have said nothing.
   */
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

    // Big enough that it would not fit beside the first batch, were that still
    // counted as on its way.
    reportLargeFailures(1, 10);
    flush();
    await vi.runAllTimersAsync();

    expect(sent.at(-1)?.keepalive).toBe(true);
  });
});

const keepaliveBudget = 64 * 1024;

/** As big as the server lets a record be, each one different. */
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
    // Without an `error` on it: the test runner treats a real one as its own
    // uncaught exception and fails the suite.
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
