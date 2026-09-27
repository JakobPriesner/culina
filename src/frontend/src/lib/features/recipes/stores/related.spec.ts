import { beforeEach, describe, expect, it, vi } from 'vitest';

import { related } from './related.svelte';

/*
 * Each recipe's shelf is asked for once, so what the store holds is what the
 * page shows for as long as the tab is open.
 */
const item = (id: string) => ({
  recipeId: id,
  title: `Recipe ${id}`,
  imageId: null,
  totalMinutes: 30,
  yieldAmount: 4,
  yieldKind: 'servings',
  tags: [],
  cookCount: 0,
  lastCookedAt: null,
  updatedAt: '2026-09-18T00:00:00Z',
  reason: { kind: 'kinds', shared: ['Italian'] }
});

/** Answers each recipe's shelf with the given ids. */
function serverAnswers(shelves: Record<string, string[]>) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request) => {
      const recipeId = /recipes\/([^/]+)\/related/.exec(input.url)?.[1] ?? '';

      return Promise.resolve(
        new Response(JSON.stringify({ items: (shelves[recipeId] ?? []).map(item) }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        })
      );
    })
  );
}

beforeEach(() => {
  related.reset();
});

describe('a deleted recipe', () => {
  it('comes off every shelf it was on, and its own shelf goes', async () => {
    serverAnswers({ r1: ['r2', 'r3'], r2: ['r1', 'r3'], r3: ['r1', 'r2'] });

    await related.load('r1');
    await related.load('r2');
    await related.load('r3');

    related.forget('r3');

    expect(related.of('r1').map((one) => one.id)).toEqual(['r2']);
    expect(related.of('r2').map((one) => one.id)).toEqual(['r1']);
    expect(related.statusOf('r3')).toBe('idle');
  });
});

/** Answers one shelf page by page, each page keyed by the cursor that asks for it. */
function serverPages(pages: Record<string, { ids: string[]; next: string | null } | 'fails'>) {
  const fetched = vi.fn((input: Request) => {
    const page = pages[new URL(input.url).searchParams.get('cursor') ?? ''];

    return Promise.resolve(
      page === 'fails' || !page
        ? new Response(JSON.stringify({ title: 'Down' }), {
            status: 503,
            headers: { 'Content-Type': 'application/problem+json' }
          })
        : new Response(JSON.stringify({ items: page.ids.map(item), nextCursor: page.next }), {
            status: 200,
            headers: { 'Content-Type': 'application/json' }
          })
    );
  });

  vi.stubGlobal('fetch', fetched);

  return fetched;
}

describe('the end of a shelf', () => {
  it('adds the next page after the ones already showing, until there is none', async () => {
    serverPages({
      '': { ids: ['r2', 'r3', 'r4'], next: 'c1' },
      c1: { ids: ['r5', 'r6', 'r7'], next: 'c2' },
      c2: { ids: ['r8'], next: null }
    });

    await related.load('r1');
    await related.more('r1');
    await related.more('r1');

    expect(related.of('r1').map((one) => one.id)).toEqual([
      'r2',
      'r3',
      'r4',
      'r5',
      'r6',
      'r7',
      'r8'
    ]);
    expect(related.hasMore('r1')).toBe(false);
  });

  it('asks once for a page, however often the end comes into view', async () => {
    const fetched = serverPages({
      '': { ids: ['r2', 'r3', 'r4'], next: 'c1' },
      c1: { ids: ['r5'], next: null }
    });

    await related.load('r1');
    await Promise.all([related.more('r1'), related.more('r1')]);

    expect(fetched).toHaveBeenCalledTimes(2);
    expect(related.of('r1').map((one) => one.id)).toEqual(['r2', 'r3', 'r4', 'r5']);
  });

  it('does not show a recipe twice when the kitchen changed between pages', async () => {
    serverPages({
      '': { ids: ['r2', 'r3', 'r4'], next: 'c1' },
      c1: { ids: ['r4', 'r5'], next: null }
    });

    await related.load('r1');
    await related.more('r1');

    expect(related.of('r1').map((one) => one.id)).toEqual(['r2', 'r3', 'r4', 'r5']);
  });

  it('keeps what it has and stops asking by itself once a page fails', async () => {
    serverPages({ '': { ids: ['r2', 'r3', 'r4'], next: 'c1' }, c1: 'fails' });

    await related.load('r1');
    await related.more('r1');

    expect(related.of('r1').map((one) => one.id)).toEqual(['r2', 'r3', 'r4']);
    expect(related.hasMore('r1')).toBe(false);
  });
});
