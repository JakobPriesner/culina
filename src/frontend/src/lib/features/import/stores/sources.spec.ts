import { beforeEach, describe, expect, it, vi } from 'vitest';

import { sources } from './sources.svelte';

/*
 * The store is where an import is actually orchestrated, so the things it can
 * get wrong are the things somebody moving eight hundred recipes would notice:
 * a batch that goes to the wrong shelf, a count that does not add up, and a
 * failure that takes the whole run down with it.
 */
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

const imported = (externalId: string) => ({
  externalId,
  outcome: 'imported',
  recipeId: `r-${externalId}`,
  title: `Recipe ${externalId}`,
  reason: null
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

describe('importing', () => {
  it('walks a big selection in batches of twenty-five', async () => {
    serverAnswers((_url, call) =>
      json({
        cookbookId: 'cb1',
        cookbookName: 'recipes.example.com · 17 September 2026',
        results: asked[call - 1]!.body.externalIds.map(imported)
      })
    );

    const chosen = Array.from({ length: 60 }, (_, index) => String(index));

    await sources.import(source.sourceId, chosen);

    expect(asked.map((one) => one.body.externalIds.length)).toEqual([25, 25, 10]);
    expect(sources.run?.imported).toBe(60);
    expect(sources.run?.done).toBe(60);
    expect(sources.run?.finished).toBe(true);
  });

  it('puts every batch on the shelf the first one made', async () => {
    serverAnswers((_url, call) =>
      json({
        cookbookId: 'cb1',
        cookbookName: 'Tandoor',
        results: asked[call - 1]!.body.externalIds.map(imported)
      })
    );

    await sources.import(
      source.sourceId,
      Array.from({ length: 30 }, (_, index) => String(index))
    );

    // Without this, a selection imported in twenty requests would be twenty
    // cookbooks — and the one thing that makes an import reviewable is that it
    // is one shelf.
    expect(asked[0]!.body.cookbookId).toBeUndefined();
    expect(asked[1]!.body.cookbookId).toBe('cb1');
    expect(sources.run?.cookbookId).toBe('cb1');
  });

  it('counts what was already here as skipped rather than failed', async () => {
    serverAnswers(() =>
      json({
        cookbookId: 'cb1',
        cookbookName: 'Tandoor',
        results: [
          imported('1'),
          { externalId: '2', outcome: 'already_here', recipeId: 'r9', title: null, reason: null }
        ]
      })
    );

    await sources.import(source.sourceId, ['1', '2']);

    // Re-running an import is the ordinary way to catch up on what is new, and
    // reporting that as two failures would make it look broken.
    expect(sources.run?.imported).toBe(1);
    expect(sources.run?.skipped).toBe(1);
    expect(sources.run?.failures).toEqual([]);
  });

  it('keeps the failures by name, so they can be shown rather than counted', async () => {
    serverAnswers(() =>
      json({
        cookbookId: 'cb1',
        cookbookName: 'Tandoor',
        results: [
          imported('1'),
          {
            externalId: '2',
            outcome: 'failed',
            recipeId: null,
            title: 'Oma’s Kuchen',
            reason: 'import.could_not_fetch'
          }
        ]
      })
    );

    await sources.import(source.sourceId, ['1', '2']);

    expect(sources.run?.failures).toEqual(['Oma’s Kuchen']);
  });

  it('carries on after a batch that failed outright', async () => {
    serverAnswers((url, call) => {
      if (call === 1) {
        return json({ code: 'import.could_not_fetch', detail: 'Nope' }, 400);
      }

      return json({
        cookbookId: 'cb1',
        cookbookName: 'Tandoor',
        results: asked[call - 1]!.body.externalIds.map(imported)
      });
    });

    await sources.import(
      source.sourceId,
      Array.from({ length: 30 }, (_, index) => String(index))
    );

    // Everything before the failed batch is already written and asking again is
    // a no-op, so losing a batch must not lose the run.
    expect(asked).toHaveLength(2);
    expect(sources.run?.failures).toHaveLength(25);
    expect(sources.run?.imported).toBe(5);
    expect(sources.run?.finished).toBe(true);
  });

  it('refuses to start a second run while one is going', async () => {
    serverAnswers((_url, call) =>
      json({
        cookbookId: 'cb1',
        cookbookName: 'Tandoor',
        results: asked[call - 1]!.body.externalIds.map(imported)
      })
    );

    const first = sources.import(source.sourceId, ['1']);
    const second = sources.import(source.sourceId, ['2']);

    await Promise.all([first, second]);

    // Two runs at once would race over one shelf and report two progress bars
    // for one intention.
    expect(asked).toHaveLength(1);
  });
});
