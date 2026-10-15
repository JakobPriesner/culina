import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { offlineReads } from './offlineReads';

/* The copies kept for reading offline, shared by the website's service worker and the installed app. */

/** A Cache Storage that holds one cache in memory, keyed by URL. */
function memoryCaches() {
  const entries = new Map<string, Response>();
  const cache = {
    match: (request: Request | string) =>
      Promise.resolve(entries.get(typeof request === 'string' ? request : request.url)?.clone()),
    put: (request: Request | string, response: Response) => {
      entries.set(typeof request === 'string' ? request : request.url, response.clone());

      return Promise.resolve();
    },
    keys: () => Promise.resolve([...entries.keys()].map((url) => new Request(url))),
    delete: (request: Request | string) =>
      Promise.resolve(entries.delete(typeof request === 'string' ? request : request.url))
  };

  return {
    open: () => Promise.resolve(cache),
    delete: () => {
      entries.clear();

      return Promise.resolve(true);
    }
  };
}

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });

const server = 'https://kitchen.example.com';
const me = new Request(`${server}/api/v1/users/me`);
const list = () => new Request(`${server}/api/v1/households/h1/shopping-list`);

let answers: Response[] = [];

/** Copies as the module keeps them, marked with whose they are; a test fetch's response is not `basic`, so is never kept. */
async function keep(path: string, body: unknown) {
  const cache = await caches.open('culina-private');
  const owned = (value: unknown) =>
    new Response(JSON.stringify(value), {
      headers: { 'Content-Type': 'application/json', 'X-Culina-Owner': 'u1' }
    });

  await cache.put(me, owned({ userId: 'u1' }));
  await cache.put(new Request(`${server}${path}`), owned(body));
}

beforeEach(() => {
  vi.stubGlobal('caches', memoryCaches());
  vi.stubGlobal(
    'fetch',
    vi.fn(() => Promise.resolve(answers.shift() ?? new Response(null, { status: 502 })))
  );
});

afterEach(() => {
  vi.unstubAllGlobals();
  answers = [];
});

const reads = () =>
  offlineReads({
    base: '',
    locate: (path) => `${server}${path}`,
    signedOut: () => Promise.resolve()
  });

describe('a server that is down behind its proxy', () => {
  it('reads the kept copy, as when there is no network at all', async () => {
    const kept = reads();
    await keep('/api/v1/households/h1/shopping-list', { items: ['Brot'] });

    for (const status of [502, 503, 504]) {
      answers = [new Response(null, { status })];

      const read = await kept.answer(list(), 'network-first', () => {});

      expect(read.status).toBe(200);
      expect(await read.json()).toEqual({ items: ['Brot'] });
    }
  });

  it('says so when nothing was kept', async () => {
    answers = [new Response(null, { status: 502 })];

    const read = await reads().answer(list(), 'network-first', () => {});

    expect(read.status).toBe(502);
  });

  it('still shows what the server says when it answers, even an error of its own', async () => {
    const kept = reads();
    await keep('/api/v1/households/h1/shopping-list', { items: ['Brot'] });
    answers = [json({ title: 'Broken', status: 500 }, 500)];

    const read = await kept.answer(list(), 'network-first', () => {});

    expect(read.status).toBe(500);
  });
});

describe('the wait for the network when a copy exists', () => {
  it('leaves no timer running once the network has answered', async () => {
    const kept = reads();
    await keep('/api/v1/households/h1/shopping-list', { items: ['Brot'] });
    answers = [json({ items: ['Milch'] })];
    vi.useFakeTimers();

    try {
      await kept.answer(list(), 'network-first', () => {});

      expect(vi.getTimerCount()).toBe(0);
    } finally {
      vi.useRealTimers();
    }
  });
});
