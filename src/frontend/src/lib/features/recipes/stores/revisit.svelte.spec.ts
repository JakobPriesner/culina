import { describe, expect, it, vi } from 'vitest';

import { cookbooks } from '$features/cookbooks/stores/cookbooks.svelte';

import { recipes } from './recipes.svelte';

/* Stores called from an $effect must not loop: a loop shows up as a flood of requests (429s). */
const json = (body: unknown) =>
  new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' }
  });

describe('a store called from an effect', () => {
  it('lists the library once', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(json({ items: [], nextCursor: null, total: 0 })))
    );

    const stop = $effect.root(() => {
      $effect(() => {
        void recipes.list('h1', { cookbookId: 'c1' });
      });
    });

    await vi.waitFor(() => expect(recipes.status).toBe('ready'));
    await new Promise((resolve) => setTimeout(resolve, 20));
    stop();

    expect(globalThis.fetch).toHaveBeenCalledTimes(1);
  });

  it('opens a cookbook once, even when it is already held', async () => {
    const detail = {
      cookbookId: 'c1',
      householdId: 'h1',
      name: 'Christmas',
      description: null,
      kind: 'manual',
      recipeCount: 0,
      coverRecipeIds: [],
      coverPictures: [],
      updatedAt: '2026-09-14T00:00:00Z',
      version: 1
    };

    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(json(detail)))
    );
    await cookbooks.load('c1');
    vi.mocked(globalThis.fetch).mockClear();

    const stop = $effect.root(() => {
      $effect(() => {
        void cookbooks.load('c1');
      });
    });

    await new Promise((resolve) => setTimeout(resolve, 30));
    stop();

    expect(globalThis.fetch).toHaveBeenCalledTimes(1);
    expect(cookbooks.open?.name).toBe('Christmas');
  });
});
