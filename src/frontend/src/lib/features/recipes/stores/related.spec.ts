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
