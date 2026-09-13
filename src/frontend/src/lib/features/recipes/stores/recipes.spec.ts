import { beforeEach, describe, expect, it, vi } from 'vitest';

import { recipes } from './recipes.svelte';

/*
 * The store is the only thing components read recipes from, so the things it
 * can get wrong are the things the whole feature gets wrong.
 */
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

const page = (
  items: ReturnType<typeof summary>[],
  total = items.length,
  nextCursor: string | null = null
) =>
  new Response(JSON.stringify({ items, nextCursor, total }), {
    status: 200,
    headers: { 'Content-Type': 'application/json' }
  });

/** Answers each request by URL, optionally after a delay. */
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
    /*
     * The bug this exists to prevent: a short query matches more rows and takes
     * longer, so the *earlier* search regularly answers last. The list then
     * settles on the wrong results and the filter looks broken.
     */
    const slow = Promise.withResolvers<Response>();

    serverAnswers((url) =>
      url.includes('query=zi') && !url.includes('query=zitronxyz') ? slow.promise : page([])
    );

    const first = recipes.list(household, { query: 'zi' });
    const second = recipes.list(household, { query: 'zitronxyz' });

    await second;

    // The stale one answers now, with two matches, and must be ignored.
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

    // Three placeholder rows come into view together, and each one asks.
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

    // The row is still there to ask for again — by hand, this time.
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

    // Those rows belong to a list that is no longer on screen.
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
