import { beforeEach, describe, expect, it, vi } from 'vitest';

import { sources } from './sources.svelte';

/*
 * The store no longer does the importing — the server does — so what it can get
 * wrong is what it makes of the stream: a count that does not add up, a
 * dropped connection reported as a finished import, and a refusal to start
 * shown as a run that never moves.
 */

/**
 * Stands in for the streamed response, so a test can be the server.
 *
 * A real `ReadableStream` behind a real `Response`, because that is what the
 * reader in `$api/events` consumes — a hand-written double of the parser would
 * only prove that the double agrees with itself.
 */
class FakeStream {
  static last: FakeStream | null = null;

  readonly response: Response;

  #push!: ReadableStreamDefaultController<Uint8Array>;
  #encoder = new TextEncoder();

  constructor(readonly headers: Headers) {
    const body = new ReadableStream<Uint8Array>({
      start: (controller) => {
        this.#push = controller;
      }
    });

    this.response = new Response(body, {
      status: 200,
      headers: { 'Content-Type': 'text/event-stream' }
    });

    FakeStream.last = this;
  }

  /** One event, framed the way the server frames it. */
  send(event: { recipe?: unknown; done: number; total: number; finished?: boolean }) {
    const data = JSON.stringify({ finished: false, ...event });

    this.#push.enqueue(this.#encoder.encode(`data: ${data}\nid: ${event.done}\n\n`));
  }

  /** What the caller asked to resume from, if anything. */
  get resumedFrom(): string | null {
    return this.headers.get('Last-Event-ID');
  }
}

/** Lets the reader's promises run before a test looks at what they did. */
const settle = () => vi.waitFor(() => expect(FakeStream.last).not.toBeNull());

const source = {
  sourceId: 's1',
  kind: 'tandoor' as const,
  label: 'recipes.example.com',
  address: 'https://recipes.example.com',
  createdAt: '2026-09-17T00:00:00Z',
  lastUsedAt: null
};

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });

const library = source;

const theirs = (externalId: string) => ({
  externalId,
  title: `Recipe ${externalId}`,
  description: null,
  imageUrl: null,
  totalMinutes: 30,
  alreadyHere: null
});

const imported = (externalId: string) => ({
  externalId,
  outcome: 'imported',
  recipeId: `r-${externalId}`,
  title: `Recipe ${externalId}`,
  reason: null
});

/**
 * Answers the POST that starts an import, and the GET that follows it.
 *
 * `streamed` decides what the follow gets: a stream to push events into, or a
 * refusal — which is the case that matters most, because a stream that is
 * refused is the difference between "still going" and "gone".
 */
function importServer(options: { start?: Response; streamed?: () => Response } = {}) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request | string, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.url;
      const method = typeof input === 'string' ? (init?.method ?? 'GET') : input.method;

      if (url.includes('/events')) {
        const headers = new Headers(init?.headers);

        return options.streamed?.() ?? new FakeStream(headers).response;
      }

      if (method === 'POST' && url.includes('/imports')) {
        asked.push({ url, body: await (input as Request).clone().json() });
      }

      return options.start ?? json(started(3));
    })
  );
}

const started = (total: number) => ({
  importId: 'i1',
  cookbookId: 'cb1',
  cookbookName: 'recipes.example.com · 17 September 2026',
  total
});

/** Every import request this test saw, in order, and how many reads there were. */
let asked: { url: string; body: { externalIds: string[]; cookbookId?: string } }[] & {
  gets: number;
} = Object.assign([], { gets: 0 });

function serverAnswers(reply: (url: string, call: number) => Response | Promise<Response>) {
  let call = 0;

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      const url = input.url;

      if (input.method === 'POST' && url.includes('/imports')) {
        asked.push({ url, body: await input.clone().json() });
      }

      if (input.method === 'GET') {
        asked.gets += 1;
      }

      call += 1;

      return reply(url, call);
    })
  );
}

beforeEach(() => {
  sources.reset();
  asked = Object.assign([], { gets: 0 });
  FakeStream.last = null;
});

describe('listing what is connected', () => {
  it('asks once per household, however many times it is called', async () => {
    serverAnswers(() => json({ items: [] }));

    await sources.list('h1');
    await sources.list('h1');
    await sources.list('h1');

    // The guard that keeps the page's `$effect` from re-triggering itself.
    // Its absence is not a slow page: it is a request per answer until the
    // rate limiter starts refusing them.
    expect(asked.gets).toBe(1);
  });

  it('asks again when the kitchen changes', async () => {
    serverAnswers(() => json({ items: [] }));

    await sources.list('h1');
    await sources.list('h2');

    expect(asked.gets).toBe(2);
  });

  it('lets a retry ask again after a failure', async () => {
    serverAnswers(() => json({ code: 'server.unavailable', detail: 'Nope' }, 503));

    await sources.list('h1');
    await sources.list('h1');

    // The second call is the effect, and it must change nothing. Only an
    // explicit retry may ask again.
    expect(asked.gets).toBe(1);

    await sources.relist('h1');

    expect(asked.gets).toBe(2);
  });
});

describe('reading more of a library', () => {
  it('stops asking once a page has failed', async () => {
    let asked = 0;

    vi.stubGlobal(
      'fetch',
      vi.fn(() => {
        asked += 1;

        if (asked === 1) {
          return Promise.resolve(json({ items: [theirs('1')], nextPage: 'p1', total: 9 }));
        }

        return Promise.resolve(json({ code: 'import.could_not_fetch' }, 400));
      })
    );

    await sources.browse(library);
    await sources.more();

    // The end of the list is what asks for the next page, and it stays on
    // screen after a failure. Without a latch this is a loop: ask, fail, ask.
    await sources.more();
    await sources.more();

    expect(asked).toBe(2);
    expect(sources.moreFailed).toBe(true);
  });

  it('keeps the pages that worked when a later one does not', async () => {
    let asked = 0;

    vi.stubGlobal(
      'fetch',
      vi.fn(() => {
        asked += 1;

        if (asked === 1) {
          return Promise.resolve(json({ items: [theirs('1')], nextPage: 'p1', total: 9 }));
        }

        return Promise.resolve(json({ code: 'import.could_not_fetch' }, 400));
      })
    );

    await sources.browse(library);
    await sources.more();

    // Replacing a screen of recipes somebody is reading with an error, because
    // the page below it could not be read, loses more than it explains.
    expect(sources.browseStatus).toBe('ready');
    expect(sources.recipes).toHaveLength(1);
  });

  it('forgets a failure when the library is read afresh', async () => {
    let asked = 0;

    vi.stubGlobal(
      'fetch',
      vi.fn(() => {
        asked += 1;

        return Promise.resolve(
          asked === 2
            ? json({ code: 'import.could_not_fetch' }, 400)
            : json({ items: [theirs('1')], nextPage: 'p1', total: 9 })
        );
      })
    );

    await sources.browse(library);
    await sources.more();

    expect(sources.moreFailed).toBe(true);

    await sources.browse(library, 'suppe');

    expect(sources.moreFailed).toBe(false);
  });
});

describe('importing', () => {
  it('asks for the whole selection at once, and follows what the server does with it', async () => {
    const chosen = Array.from({ length: 12 }, (_, index) => String(index));

    importServer({ start: json(started(chosen.length)) });

    await sources.import(source.sourceId, chosen);
    await settle();

    // One request that names the work, rather than twelve that do it: the
    // pacing is the server's business now, and nothing is lost by closing the
    // tab a second later.
    expect(asked).toHaveLength(1);
    expect(asked[0]!.body.externalIds).toEqual(chosen);
  });

  it('knows the shelf before the first recipe arrives', async () => {
    importServer();

    await sources.import(source.sourceId, ['1', '2', '3']);

    // The whole reason somebody may walk away from this screen: the way back
    // exists from the beginning.
    expect(sources.run?.cookbookId).toBe('cb1');
    expect(sources.run?.total).toBe(3);
    expect(sources.run?.done).toBe(0);
  });

  it('counts each recipe as the stream reports it', async () => {
    importServer({ start: json(started(2)) });

    await sources.import(source.sourceId, ['1', '2']);
    await settle();

    FakeStream.last!.send({ recipe: imported('1'), done: 1, total: 2 });

    await vi.waitFor(() => expect(sources.run?.done).toBe(1));
    expect(sources.run?.finished).toBe(false);

    FakeStream.last!.send({ recipe: imported('2'), done: 2, total: 2 });
    FakeStream.last!.send({ done: 2, total: 2, finished: true });

    await vi.waitFor(() => expect(sources.run?.finished).toBe(true));
    expect(sources.run?.imported).toBe(2);
  });

  it('counts what was already here as skipped rather than failed', async () => {
    importServer({ start: json(started(2)) });

    await sources.import(source.sourceId, ['1', '2']);
    await settle();

    FakeStream.last!.send({ recipe: imported('1'), done: 1, total: 2 });
    FakeStream.last!.send({
      recipe: { externalId: '2', outcome: 'already_here', recipeId: 'r9' },
      done: 2,
      total: 2
    });

    // Re-running an import is the ordinary way to catch up on what is new, and
    // reporting that as a failure would make it look broken.
    await vi.waitFor(() => expect(sources.run?.done).toBe(2));
    expect(sources.run?.imported).toBe(1);
    expect(sources.run?.skipped).toBe(1);
    expect(sources.run?.failures).toEqual([]);
  });

  it('keeps the failures by name, so they can be shown rather than counted', async () => {
    importServer({ start: json(started(2)) });

    await sources.import(source.sourceId, ['1', '2']);
    await settle();

    FakeStream.last!.send({
      recipe: {
        externalId: '2',
        outcome: 'failed',
        title: 'Oma’s Kuchen',
        reason: 'import.could_not_fetch'
      },
      done: 1,
      total: 2
    });

    await vi.waitFor(() => expect(sources.run?.failures).toEqual(['Oma’s Kuchen']));
  });

  it('ignores the ticks that only keep the connection open', async () => {
    importServer({ start: json(started(2)) });

    await sources.import(source.sourceId, ['1', '2']);
    await settle();

    FakeStream.last!.send({ recipe: imported('1'), done: 1, total: 2 });
    FakeStream.last!.send({ done: 1, total: 2 });

    await vi.waitFor(() => expect(sources.run?.done).toBe(1));
    expect(sources.run?.imported).toBe(1);
  });

  it('says why it stopped following, rather than calling the import finished', async () => {
    importServer({
      start: json(started(2)),
      streamed: () => json({ code: 'import.import_not_found', detail: 'That import is gone.' }, 404)
    });

    await sources.import(source.sourceId, ['1', '2']);

    // A refusal is an answer: it is reported at once rather than retried, and
    // it carries the reason, because "the connection went" and "that import is
    // gone" are two situations and only one is worth waiting through.
    await vi.waitFor(() => expect(sources.run?.lost?.code).toBe('import.import_not_found'));
    expect(sources.run?.finished).toBe(false);
    expect(sources.importing).toBe(false);
  });

  it('picks the stream back up from where it stopped, not from the beginning', async () => {
    importServer({ start: json(started(3)) });

    await sources.import(source.sourceId, ['1', '2', '3']);
    await settle();

    FakeStream.last!.send({ recipe: imported('1'), done: 1, total: 3 });
    FakeStream.last!.send({ recipe: imported('2'), done: 2, total: 3 });

    await vi.waitFor(() => expect(sources.run?.done).toBe(2));

    const first = FakeStream.last;

    sources.reconnect();

    await vi.waitFor(() => expect(FakeStream.last).not.toBe(first));

    // The server numbers every event with how many outcomes it has sent, so
    // this asks for what is missing and nothing else — the two already counted
    // are neither repeated nor forgotten.
    expect(FakeStream.last!.resumedFrom).toBe('2');
    expect(sources.run?.imported).toBe(2);
    expect(sources.run?.lost).toBeNull();
  });

  it('shows a refusal to start beside the selection, rather than as a run', async () => {
    importServer({ start: json({ code: 'import.too_many_at_once', detail: 'Nope' }, 400) });

    await sources.import(source.sourceId, ['1', '2']);

    // Nothing was started, so there is nothing to watch — and the selection is
    // still on screen to be asked for again.
    expect(sources.run).toBeNull();
    expect(sources.importError?.code).toBe('import.too_many_at_once');
    expect(FakeStream.last).toBeNull();
  });

  it('refuses to start a second run while one is going', async () => {
    importServer({ start: json(started(1)) });

    const first = sources.import(source.sourceId, ['1']);
    const second = sources.import(source.sourceId, ['2']);

    await Promise.all([first, second]);

    // Two runs at once would race over one shelf and report two progress bars
    // for one intention.
    expect(asked).toHaveLength(1);
  });
});
