import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { shopping } from '$features/shopping/stores/shopping.svelte';

import { addShelfToShoppingList } from './addShelfToShoppingList';

// The shelf on screen is only its first page, so adding it must read past that page.
const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });

const wire = (id: string, yieldAmount = 4) => ({
  recipeId: id,
  householdId: 'h1',
  title: id,
  yieldAmount,
  yieldKind: 'servings',
  tags: [],
  cookCount: 0,
  updatedAt: '2026-09-14T00:00:00Z'
});

function server(
  pages: ReturnType<typeof wire>[][],
  {
    failing = [],
    failPagingAfterFirst = false
  }: { failing?: string[]; failPagingAfterFirst?: boolean } = {}
) {
  const added: { recipeId: string; servings: number }[] = [];
  const asked: string[] = [];

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      const url = new URL(input.url);

      if (input.method === 'GET' && url.pathname === '/api/v1/recipes') {
        asked.push(url.searchParams.get('cookbookId') ?? '');

        if (failPagingAfterFirst && url.searchParams.get('cursor')) {
          return json({ title: 'Down', status: 500 }, 500);
        }

        const page = Number(url.searchParams.get('cursor') ?? 0);
        const items = pages[page] ?? [];

        return json({
          items,
          nextCursor: page + 1 < pages.length ? String(page + 1) : null,
          total: pages.flat().length
        });
      }

      const body = (await input.json()) as { recipeId: string; servings: number };

      if (failing.includes(body.recipeId)) {
        return json({ title: 'Nope', status: 500 }, 500);
      }

      added.push(body);

      return json({ items: [] });
    })
  );

  return { added, asked };
}

beforeEach(() => shopping.reset());

afterEach(() => vi.unstubAllGlobals());

describe('adding a whole shelf to the shopping list', () => {
  it('puts every recipe on, not just the first page of the shelf', async () => {
    const { added, asked } = server([[wire('r1'), wire('r2', 2)], [wire('r3')], [wire('r4', 6)]]);

    const result = await addShelfToShoppingList('h1', 'c1');

    expect(result).toEqual({ done: 4, total: 4 });
    expect(added).toEqual([
      { recipeId: 'r1', servings: 4 },
      { recipeId: 'r2', servings: 2 },
      { recipeId: 'r3', servings: 4 },
      { recipeId: 'r4', servings: 6 }
    ]);
    expect(new Set(asked)).toEqual(new Set(['c1']));
  });

  it('reports how many made it when some recipes fail, and carries on past them', async () => {
    const { added } = server([[wire('r1'), wire('r2'), wire('r3')]], { failing: ['r2'] });

    const result = await addShelfToShoppingList('h1', 'c1');

    expect(result).toEqual({ done: 2, total: 3 });
    expect(added.map((one) => one.recipeId)).toEqual(['r1', 'r3']);
  });

  it('adds nothing, and says why, when the shelf cannot be read to its end', async () => {
    const { added } = server([[wire('r1')], [wire('r2')]], { failPagingAfterFirst: true });

    const result = await addShelfToShoppingList('h1', 'c1');

    expect('done' in result).toBe(false);
    expect(added).toEqual([]);
  });

  it('has nothing to add for an empty shelf', async () => {
    server([[]]);

    expect(await addShelfToShoppingList('h1', 'c1')).toEqual({ done: 0, total: 0 });
  });
});
