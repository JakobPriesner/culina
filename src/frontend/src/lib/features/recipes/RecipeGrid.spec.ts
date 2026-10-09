import { screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import RecipeGrid from './RecipeGrid.svelte';
import type { RecipeSummary } from './types';
import { renderWithProviders } from '$lib/test/render';

/* Only the list asks for the next page: reaching the end must ask, and nothing asks when nothing is left. */
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

describe('the photos of the list', () => {
  it('ranks only the first two ahead of the rest', () => {
    const photographed = ['r1', 'r2', 'r3', 'r4'].map((id) => ({
      ...recipe(id),
      imageId: `i-${id}`
    }));
    const view = renderWithProviders(RecipeGrid, { props: { recipes: photographed } });
    const images = [...view.container.querySelectorAll('img')];

    expect(images.map((img) => img.getAttribute('fetchpriority'))).toEqual([
      'high',
      'high',
      null,
      null
    ]);
    expect(images.map((img) => img.getAttribute('loading'))).toEqual([
      'eager',
      'eager',
      'lazy',
      'lazy'
    ]);
  });
});
