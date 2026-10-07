import { beforeEach, describe, expect, it, vi } from 'vitest';

import { recipes } from './recipes.svelte';

/* The store is the only thing components read recipes from. */
const household = 'h1';

const summary = (id: string, title: string) => ({
  recipeId: id,
  title,
  imageId: null,
  totalMinutes: 25,
  yieldAmount: 4,
  yieldKind: 'servings',
  tags: ['Weeknight'],
  cookCount: 0,
  updatedAt: '2026-09-12T00:00:00Z',
  ingredientMatch: null
});

const recipe = (id: string, title: string) => ({
  recipeId: id,
  householdId: household,
  title,
  language: 'en',
  yieldAmount: 4,
  yieldKind: 'servings',
  groups: [],
  steps: [],
  tags: [],
  createdBy: 'u1',
  createdAt: '2026-09-12T00:00:00Z',
  updatedAt: '2026-09-12T00:00:00Z',
  version: 3
});

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });

const page = (
  items: ReturnType<typeof summary>[],
  total = items.length,
  nextCursor: string | null = null
) =>
  new Response(JSON.stringify({ items, nextCursor, total }), {
    status: 200,
    headers: { 'Content-Type': 'application/json' }
  });

function serverAnswers(reply: (url: string) => Response | Promise<Response>) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request) => Promise.resolve(reply(input.url)))
  );
}

beforeEach(() => {
  recipes.reset();
  serverAnswers(() => page([]));
});

describe('listing', () => {
  it('reads the page into the store', async () => {
    serverAnswers(() => page([summary('r1', 'Orzo'), summary('r2', 'Soup')]));

    await recipes.list(household);

    expect(recipes.items.map((item) => item.title)).toEqual(['Orzo', 'Soup']);
    expect(recipes.total).toBe(2);
    expect(recipes.status).toBe('ready');
  });

  it('reports a failure without emptying what is on screen', async () => {
    serverAnswers(() => page([summary('r1', 'Orzo')]));
    await recipes.list(household);

    serverAnswers(
      () =>
        new Response(JSON.stringify({ code: 'recipes.unavailable', detail: 'No.' }), {
          status: 500,
          headers: { 'Content-Type': 'application/problem+json' }
        })
    );

    await recipes.list(household);

    expect(recipes.status).toBe('failed');
    expect(recipes.error?.code).toBe('recipes.unavailable');
  });
});

describe('two searches in flight at once', () => {
  it('keeps the newer answer, however late the older one arrives', async () => {
    // A short query matches more rows and takes longer, so the earlier search answers last; the
    // list must not settle on it.
    const slow = Promise.withResolvers<Response>();

    serverAnswers((url) =>
      url.includes('query=zi') && !url.includes('query=zitronxyz') ? slow.promise : page([])
    );

    const first = recipes.list(household, { query: 'zi' });
    const second = recipes.list(household, { query: 'zitronxyz' });

    await second;

    slow.resolve(page([summary('r1', 'Orzo'), summary('r2', 'Zitronensuppe')]));
    await first;

    expect(recipes.items).toEqual([]);
    expect(recipes.total).toBe(0);
  });

  it('does not leave the list stuck loading when the stale one loses', async () => {
    const slow = Promise.withResolvers<Response>();

    serverAnswers((url) =>
      url.includes('query=a&') ? slow.promise : page([summary('r1', 'Orzo')])
    );

    const first = recipes.list(household, { query: 'a' });
    const second = recipes.list(household, { query: 'ab' });

    await second;
    slow.resolve(page([]));
    await first;

    expect(recipes.status).toBe('ready');
    expect(recipes.items).toHaveLength(1);
  });
});

describe('the next page', () => {
  it('is appended, so the rows already read do not move', async () => {
    serverAnswers(() => page([summary('r1', 'Orzo')], 2, 'cursor-1'));
    await recipes.list(household);

    serverAnswers(() => page([summary('r2', 'Soup')], 2, null));
    await recipes.loadMore(household);

    expect(recipes.items.map((item) => item.title)).toEqual(['Orzo', 'Soup']);
    expect(recipes.hasMore).toBe(false);
  });

  it('is asked for once, however often the end of the list is reached', async () => {
    serverAnswers(() => page([summary('r1', 'Orzo')], 9, 'cursor-1'));
    await recipes.list(household);

    const slow = Promise.withResolvers<Response>();
    const server = vi.fn((input: Request) =>
      input.url.includes('cursor') ? slow.promise : Promise.resolve(page([]))
    );

    vi.stubGlobal('fetch', server);

    const asks = [
      recipes.loadMore(household),
      recipes.loadMore(household),
      recipes.loadMore(household)
    ];

    slow.resolve(page([summary('r2', 'Soup')], 9, null));
    await Promise.all(asks);

    expect(server).toHaveBeenCalledTimes(1);
    expect(recipes.items).toHaveLength(2);
  });

  it('stops asking once a page fails, so a dead connection is not a loop', async () => {
    serverAnswers(() => page([summary('r1', 'Orzo')], 9, 'cursor-1'));
    await recipes.list(household);

    serverAnswers(() => new Response('', { status: 500 }));
    await recipes.loadMore(household);

    expect(recipes.moreFailed).toBe(true);
    expect(recipes.items).toHaveLength(1);

    serverAnswers(() => page([summary('r2', 'Soup')], 9, null));
    await recipes.loadMore(household);

    expect(recipes.moreFailed).toBe(false);
    expect(recipes.items.map((item) => item.title)).toEqual(['Orzo', 'Soup']);
  });

  it('forgets a failed page when the filter changes', async () => {
    serverAnswers(() => page([summary('r1', 'Orzo')], 9, 'cursor-1'));
    await recipes.list(household);

    serverAnswers(() => new Response('', { status: 500 }));
    await recipes.loadMore(household);

    serverAnswers(() => page([summary('r9', 'Other')]));
    await recipes.list(household, { query: 'other' });

    expect(recipes.moreFailed).toBe(false);
  });

  it('is discarded when the filter changed while it was in flight', async () => {
    serverAnswers(() => page([summary('r1', 'Orzo')], 9, 'cursor-1'));
    await recipes.list(household);

    const slow = Promise.withResolvers<Response>();

    serverAnswers((url) =>
      url.includes('cursor') ? slow.promise : page([summary('r9', 'Other')])
    );

    const more = recipes.loadMore(household);
    const search = recipes.list(household, { query: 'other' });

    await search;
    slow.resolve(page([summary('r2', 'Soup')], 9, null));
    await more;

    expect(recipes.items.map((item) => item.title)).toEqual(['Other']);
  });
});

describe('signing out', () => {
  it('leaves nothing for the next person', async () => {
    serverAnswers(() => page([summary('r1', 'Orzo')]));
    await recipes.list(household);

    recipes.reset();

    expect(recipes.items).toEqual([]);
    expect(recipes.detail).toBeNull();
    expect(recipes.status).toBe('idle');
  });
});

describe('opening a recipe', () => {
  it('shows the recipe asked for last, however late the one before answers', async () => {
    const slow = Promise.withResolvers<Response>();

    serverAnswers((url) => (url.endsWith('/recipes/a') ? slow.promise : json(recipe('b', 'Soup'))));

    const first = recipes.load('a');
    await recipes.load('b');

    slow.resolve(json(recipe('a', 'Orzo')));
    await first;

    expect(recipes.detail?.id).toBe('b');
    expect(recipes.detailStatus).toBe('ready');
  });

  it('is not marked failed by a library read that fails after it opened', async () => {
    const slow = Promise.withResolvers<Response>();

    serverAnswers((url) =>
      url.includes('/recipes/r1') ? json(recipe('r1', 'Orzo')) : slow.promise
    );

    const listing = recipes.list(household);
    await recipes.load('r1');

    slow.resolve(new Response('', { status: 500 }));
    await listing;

    expect(recipes.status).toBe('failed');
    expect(recipes.detailStatus).toBe('ready');
    expect(recipes.detailError).toBeNull();
  });

  it('stays not found when a library read succeeds after it', async () => {
    const slow = Promise.withResolvers<Response>();

    serverAnswers((url) =>
      url.includes('/recipes/gone') ? json({ code: 'recipes.not_found' }, 404) : slow.promise
    );

    const listing = recipes.list(household);
    await recipes.load('gone');

    slow.resolve(page([]));
    await listing;

    expect(recipes.detailStatus).toBe('failed');
    expect(recipes.detailError?.status).toBe(404);
  });
});

describe('deleting', () => {
  async function openOrzo(deleted: () => Response) {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: Request) => {
        if (input.method === 'DELETE') {
          return Promise.resolve(deleted());
        }

        return Promise.resolve(
          input.url.includes('/recipes/r1')
            ? json(recipe('r1', 'Orzo'))
            : page([summary('r1', 'Orzo'), summary('r2', 'Soup')])
        );
      })
    );

    await recipes.list(household);
    await recipes.load('r1');
  }

  it('lets go of the open recipe once it is gone, so it is not drawn again', async () => {
    await openOrzo(() => new Response(null, { status: 204 }));

    const failure = await recipes.remove('r1', 3);

    expect(failure).toBeNull();
    expect(recipes.detail).toBeNull();
    expect(recipes.items.map((item) => item.title)).toEqual(['Soup']);
    expect(recipes.total).toBe(1);
  });

  it('puts everything back when the server keeps it', async () => {
    await openOrzo(
      () =>
        new Response(JSON.stringify({ code: 'recipes.unavailable', detail: 'No.' }), {
          status: 500,
          headers: { 'Content-Type': 'application/problem+json' }
        })
    );

    const failure = await recipes.remove('r1', 3);

    expect(failure?.code).toBe('recipes.unavailable');
    expect(recipes.detail?.title).toBe('Orzo');
    expect(recipes.items.map((item) => item.title)).toEqual(['Orzo', 'Soup']);
    expect(recipes.total).toBe(2);
  });
});

describe('switching households', () => {
  it('lets go of the other household’s recipes before this one’s arrive', async () => {
    serverAnswers(() => page([summary('r1', 'Their orzo')]));
    await recipes.list('h-theirs');

    let answer: (response: Response) => void = () => undefined;
    const pending = new Promise<Response>((resolve) => (answer = resolve));
    serverAnswers(() => pending);

    const reading = recipes.list('h-ours');

    expect(recipes.items).toEqual([]);
    expect(recipes.status).toBe('loading');

    answer(page([summary('r2', 'Our soup')]));
    await reading;

    expect(recipes.items.map((item) => item.title)).toEqual(['Our soup']);
  });

  it('keeps the list on screen while the same household is asked again', async () => {
    serverAnswers(() => page([summary('r1', 'Orzo')]));
    await recipes.list(household);

    let answer: (response: Response) => void = () => undefined;
    const pending = new Promise<Response>((resolve) => (answer = resolve));
    serverAnswers(() => pending);

    const reading = recipes.list(household, { query: 'orz' });

    expect(recipes.items.map((item) => item.title)).toEqual(['Orzo']);

    answer(page([summary('r1', 'Orzo')]));
    await reading;
  });
});
