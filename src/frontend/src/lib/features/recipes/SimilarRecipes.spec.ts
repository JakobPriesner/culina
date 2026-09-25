import { screen } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import SimilarRecipes from './SimilarRecipes.svelte';
import { related } from './stores/related.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * The shelf of recipes like the one being read.
 *
 * Rendered rather than tested through the store, because the thing most
 * likely to go wrong is the pairing of the two: a store that guarded itself
 * with `$state` would make the effect that calls it ask forever, and nothing
 * about the store on its own would be wrong.
 */
const item = (id: string, title: string, kind: 'kinds' | 'ingredients', shared: string[]) => ({
  recipeId: id,
  title,
  imageId: null,
  totalMinutes: 30,
  yieldAmount: 4,
  yieldKind: 'servings',
  tags: [],
  cookCount: 0,
  lastCookedAt: null,
  updatedAt: '2026-09-18T00:00:00Z',
  reason: { kind, shared }
});

function serverAnswers(items: unknown[]) {
  const fetched = vi.fn(() =>
    Promise.resolve(
      new Response(JSON.stringify({ items }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
    )
  );

  vi.stubGlobal('fetch', fetched);

  return fetched;
}

const settle = async () => {
  for (let turn = 0; turn < 6; turn += 1) {
    await new Promise((resume) => setTimeout(resume, 0));
  }
};

beforeEach(() => {
  related.reset();
});

describe('recipes like this one', () => {
  it('says why each one is there', async () => {
    serverAnswers([
      item('r2', 'Lasagne Bolognese', 'kinds', ['Bolognese']),
      item('r3', 'Ragù alla Napoletana', 'kinds', ['Italian', 'baked']),
      item('r4', 'Chili con Carne', 'ingredients', ['mince', 'tomato', 'onion'])
    ]);

    renderWithProviders(SimilarRecipes, { props: { recipeId: 'r1' } });
    await settle();

    // A suggestion whose reason is shown is one somebody can disagree with.
    expect(screen.getByText('Also: Bolognese')).toBeInTheDocument();
    expect(screen.getByText('Also: Italian and baked')).toBeInTheDocument();
    expect(screen.getByText('Shares mince, tomato, and onion')).toBeInTheDocument();
  });

  it('asks once for the recipe being read', async () => {
    const fetched = serverAnswers([]);

    renderWithProviders(SimilarRecipes, { props: { recipeId: 'r1' } });
    await settle();

    // One, not "a reasonable number": a second is an effect that re-triggered
    // itself, and the next one after that is the rate limiter.
    expect(fetched).toHaveBeenCalledTimes(1);
    expect((fetched.mock.calls[0] as unknown as [Request])[0].url).toContain('/recipes/r1/related');
  });

  it('shows nothing at all below three', async () => {
    serverAnswers([
      item('r2', 'Lasagne Bolognese', 'kinds', ['Bolognese']),
      item('r3', 'Ragù alla Napoletana', 'kinds', ['Italian'])
    ]);

    renderWithProviders(SimilarRecipes, { props: { recipeId: 'r1' } });
    await settle();

    // The page is complete without the shelf, so two weak matches would have
    // to earn a place that its absence costs nothing.
    expect(screen.queryByRole('heading', { name: 'Similar recipes' })).not.toBeInTheDocument();
  });
});
