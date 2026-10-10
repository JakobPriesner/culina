import { screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { Recipe } from '$features/recipes/types';
import { renderWithProviders } from '$lib/test/render';
import { preferences } from '$shell/preferences.svelte';

import EditorNutritionLine from './EditorNutritionLine.svelte';
import { nutrition } from './stores/nutrition.svelte';

const recipe = {
  id: 'r1',
  householdId: 'h1',
  version: 3,
  groups: [
    {
      id: 'g1',
      name: null,
      ingredients: [
        { id: 'a', name: 'butter' },
        { id: 'b', name: 'onion' },
        { id: 'c', name: 'salt' }
      ]
    }
  ]
} as unknown as Recipe;

const value = { value: 1, atLeast: false };
const answer = (over: object) => ({
  per: 'serving',
  yield: 2,
  complete: false,
  counted: 1,
  lines: 3,
  values: Object.fromEntries(
    [
      'energyKj',
      'energyKcal',
      'fat',
      'saturatedFat',
      'carbohydrate',
      'sugars',
      'protein',
      'salt'
    ].map((name) => [name, value])
  ),
  ingredients: [
    { ingredientId: 'a', status: 'counted', corrected: false, canRaiseEnergy: false },
    {
      ingredientId: 'b',
      status: 'amountNotInGrams',
      reason: 'count',
      corrected: false,
      canRaiseEnergy: true
    },
    { ingredientId: 'c', status: 'noAmount', corrected: false, canRaiseEnergy: true }
  ],
  source: { name: 'BLS', version: '4.0', publisher: 'MRI', licence: 'CC BY 4.0' },
  ...over
});

const reply = (body: unknown, status = 200) =>
  vi.fn(() =>
    Promise.resolve(
      new Response(JSON.stringify(body), {
        status,
        headers: { 'Content-Type': 'application/json' }
      })
    )
  );

const show = (unsaved = false, version = 3) =>
  renderWithProviders(EditorNutritionLine, {
    props: { recipe: { ...recipe, version }, unsaved }
  });

beforeEach(() => preferences.setLocale('en'));

afterEach(() => {
  nutrition.reset();
  vi.unstubAllGlobals();
});

describe('the editor line about what counts', () => {
  it('says how many lines count and names what does not', async () => {
    vi.stubGlobal('fetch', reply(answer({})));
    show();

    expect(
      await screen.findByText('Nutrition: 1 of 3 lines count · onion: a count · salt: no amount')
    ).toBeInTheDocument();
    expect(screen.queryByText('as last saved', { exact: false })).not.toBeInTheDocument();
  });

  it('says every line counts', async () => {
    vi.stubGlobal('fetch', reply(answer({ complete: true, counted: 3 })));
    show();

    expect(await screen.findByText('Nutrition: every line counts')).toBeInTheDocument();
  });

  it('says it describes the last save while there is typing the server has not seen', async () => {
    vi.stubGlobal('fetch', reply(answer({})));
    show(true);

    expect(await screen.findByText(/\(as last saved\)/)).toBeInTheDocument();
  });

  it('asks again when a save has made a new version', async () => {
    const fetched = reply(answer({}));

    vi.stubGlobal('fetch', fetched);

    const view = show(false, 3);

    await screen.findByText(/1 of 3 lines count/);
    expect(fetched).toHaveBeenCalledTimes(1);

    await view.rerender({ recipe: { ...recipe, version: 4 }, unsaved: false });

    await waitFor(() => expect(fetched).toHaveBeenCalledTimes(2));
  });

  it('is absent when the answer is not there, and never says why', async () => {
    vi.stubGlobal('fetch', reply({}, 503));
    const { container } = show();

    await waitFor(() => expect(nutrition.statusFor('r1', 'h1')).toBe('failed'));

    expect(container.querySelector('.coverage')).toBeNull();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
