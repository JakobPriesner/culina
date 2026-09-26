import { screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import RecipeGrid from './RecipeGrid.svelte';
import type { RecipeSummary } from './types';
import { renderWithProviders } from '$lib/test/render';

/*
 * The list is the only thing that asks for the next page, so the thing to
 * prove here is that reaching the end asks — and that nothing asks when there
 * is nothing left to read.
 */
const recipe = (id: string): RecipeSummary => ({
  id,
  title: `Recipe ${id}`,
  imageId: null,
  totalMinutes: 25,
  yieldAmount: 4,
  yieldKind: 'servings',
  yieldLabel: null,
  tags: [],
  cookCount: 0,
  lastCookedAt: null,
  updatedAt: '2026-09-12T00:00:00Z',
  match: null
});

/** Every element the grid asked the browser to watch, and a way to reach it. */
let watched: (() => void)[] = [];

function stubObserver() {
  watched = [];

  vi.stubGlobal(
    'IntersectionObserver',
    class {
      #callback: IntersectionObserverCallback;

      constructor(callback: IntersectionObserverCallback) {
        this.#callback = callback;
      }

      observe(element: Element) {
        watched.push(() =>
          this.#callback(
            [{ isIntersecting: true, target: element } as IntersectionObserverEntry],
            this as unknown as IntersectionObserver
          )
        );
      }

      disconnect() {}
      unobserve() {}
      takeRecords() {
        return [];
      }
    }
  );
}

afterEach(() => vi.unstubAllGlobals());

describe('the end of the recipe list', () => {
  it('asks for the next page when it is reached', () => {
    stubObserver();

    const more = vi.fn();

    renderWithProviders(RecipeGrid, { props: { recipes: [recipe('r1')], onmore: more } });

    expect(watched.length).toBeGreaterThan(0);

    for (const reach of watched) {
      reach();
    }

    expect(more).toHaveBeenCalled();
  });

  it('watches nothing when there is no next page', () => {
    stubObserver();

    renderWithProviders(RecipeGrid, { props: { recipes: [recipe('r1')] } });

    expect(watched).toHaveLength(0);
  });

  it('hides the rows that have not arrived from a screen reader', () => {
    stubObserver();

    renderWithProviders(RecipeGrid, { props: { recipes: [recipe('r1')], onmore: vi.fn() } });

    // One row is readable; the placeholders below it are not rows yet.
    expect(screen.getAllByRole('listitem')).toHaveLength(1);
  });
});

describe('recipes from another household', () => {
  it('says where an inherited recipe comes from, and nothing on the household’s own', () => {
    stubObserver();

    renderWithProviders(RecipeGrid, {
      props: {
        recipes: [
          { ...recipe('own'), householdId: 'h-flat' },
          { ...recipe('inherited'), householdId: 'h-family' }
        ],
        inherited: { 'h-family': 'Family' }
      }
    });

    const [own, inherited] = screen.getAllByRole('listitem');

    expect(inherited).toHaveTextContent('From Family');
    expect(own).not.toHaveTextContent('From');
  });
});
