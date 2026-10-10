import { waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import RecipePage from './+page.svelte';
import { cookLog } from '$features/cooking/stores/cookLog.svelte';
import { notes } from '$features/cooking/stores/notes.svelte';
import { recipes } from '$features/recipes/stores/recipes.svelte';
import { related } from '$features/recipes/stores/related.svelte';
import { renderWithProviders } from '$lib/test/render';

vi.mock('$app/state', () => ({
  page: { params: { recipeId: 'r1' }, url: new URL('http://localhost/recipes/r1') }
}));

/* The note, the attempts and the similar shelf belong to the id, so they are asked for beside the recipe, not once it has drawn them. */
const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

const wireRecipe = {
  recipeId: 'r1',
  householdId: 'h1',
  title: 'Lemon orzo',
  language: 'en',
  yieldAmount: 4,
  yieldKind: 'servings',
  groups: [],
  steps: [],
  tags: [],
  createdBy: 'u1',
  createdAt: '2026-09-12T00:00:00Z',
  updatedAt: '2026-09-12T00:00:00Z',
  version: 1
};

const answers: Record<string, unknown> = {
  '/recipes/r1/notes': { overall: 'Use the heavy pan', steps: [] },
  '/recipes/r1/cook-log': { count: 0, lastMadeAt: null, items: [] },
  '/recipes/r1/related': { items: [] }
};

let releaseRecipe: () => void = () => {};
let asked: string[] = [];

beforeEach(() => {
  asked = [];
  vi.stubGlobal(
    'IntersectionObserver',
    class {
      observe() {}
      disconnect() {}
    }
  );
  vi.stubGlobal(
    'fetch',
    vi.fn((request: Request) => {
      const path = new URL(request.url).pathname.replace('/api/v1', '');

      asked.push(path);

      if (path === '/recipes/r1') {
        return new Promise<Response>(
          (resolve) => (releaseRecipe = () => resolve(json(wireRecipe)))
        );
      }

      // Not what this page spec is about; unavailable is the panel's ordinary state.
      if (path.endsWith('/nutrition')) {
        return Promise.resolve(new Response(null, { status: 404 }));
      }

      return Promise.resolve(path in answers ? json(answers[path]) : json({ items: [] }));
    })
  );
});

afterEach(() => {
  notes.reset();
  cookLog.reset();
  related.reset();
  recipes.reset();
  vi.unstubAllGlobals();
});

const count = (path: string) => asked.filter((asked) => asked === path).length;

describe('the recipe page', () => {
  it('reads the note, the attempts and the similar shelf while the recipe is still arriving', async () => {
    renderWithProviders(RecipePage);

    await waitFor(() => {
      expect(count('/recipes/r1')).toBe(1);
      expect(count('/recipes/r1/notes')).toBe(1);
      expect(count('/recipes/r1/cook-log')).toBe(1);
      expect(count('/recipes/r1/related')).toBe(1);
    });
  });

  it('reads each of them exactly once, also after the recipe has drawn its panels', async () => {
    renderWithProviders(RecipePage);

    await waitFor(() => expect(count('/recipes/r1')).toBe(1));
    releaseRecipe();
    await waitFor(() => expect(recipes.detail?.id).toBe('r1'));
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(count('/recipes/r1')).toBe(1);
    expect(count('/recipes/r1/notes')).toBe(1);
    expect(count('/recipes/r1/cook-log')).toBe(1);
    expect(count('/recipes/r1/related')).toBe(1);
    expect(notes.overall).toBe('Use the heavy pan');
  });
});
