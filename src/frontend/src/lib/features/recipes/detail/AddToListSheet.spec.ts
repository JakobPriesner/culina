import { screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { upcoming } from '$features/planning/upcoming.svelte';
import { shopping } from '$features/shopping/stores/shopping.svelte';
import { renderWithProviders } from '$lib/test/render';

import type { Recipe } from '../types';
import AddToListSheet from './AddToListSheet.svelte';

/* Shopping for a recipe: what is already there starts unticked, the cupboard can be left out, and a planned meal keeps its provenance. */
const quantity = (value: number, unit: string | null) => ({ value, unit });
const recipe = {
  id: 'r1',
  householdId: 'h1',
  title: 'Pancakes',
  language: 'en',
  yieldAmount: 2,
  yieldKind: 'servings',
  yieldLabel: null,
  groups: [
    {
      id: 'g1',
      name: null,
      ingredients: [
        { id: 'flour', name: 'Flour', quantity: quantity(200, 'g'), note: null },
        { id: 'milk', name: 'Milk', quantity: quantity(300, 'ml'), note: null }
      ]
    }
  ],
  steps: [],
  tags: []
} as unknown as Recipe;

let posted: { url: string; body: Record<string, unknown> }[] = [];
let plannedDays: unknown[] = [];

beforeEach(() => {
  posted = [];
  plannedDays = [];
  HTMLDialogElement.prototype.showModal = function (this: HTMLDialogElement) {
    this.open = true;
  };
  HTMLDialogElement.prototype.close = function (this: HTMLDialogElement) {
    this.open = false;
  };
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      const url = new URL(input.url);
      const json = (body: unknown) =>
        new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

      if (input.method === 'POST') {
        posted.push({ url: url.pathname, body: await input.json() });
        return json({ items: [], version: 2 });
      }
      if (url.pathname.endsWith('/meal-plan')) {
        return json({ from: url.searchParams.get('from'), days: plannedDays });
      }
      return json({
        items: [
          {
            itemId: 'i1',
            name: 'milk',
            isChecked: false,
            sources: [{ recipeId: 'r1', recipeTitle: 'Pancakes' }]
          },
          {
            itemId: 'i2',
            name: 'flour',
            isChecked: false,
            sources: [{ recipeId: 'r2', recipeTitle: 'Bread' }]
          }
        ],
        version: 1
      });
    })
  );
});

afterEach(() => {
  shopping.reset();
  upcoming.reset();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

const open = () =>
  renderWithProviders(AddToListSheet, {
    props: { open: true, householdId: 'h1', recipe, servings: 2, onclose: vi.fn() }
  });

describe('putting a recipe on the shopping list', () => {
  it('leaves out what this recipe already listed, but adds to another recipe’s line', async () => {
    open();

    const milk = await screen.findByRole('checkbox', { name: /Milk/ });
    await waitFor(() => expect(milk).not.toBeChecked());
    expect(screen.getByText('already on the list for this recipe')).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /Flour/ })).toBeChecked();
    expect(screen.getByText('already on the list; this adds to it')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Add 1 ingredient' }));

    expect(posted).toEqual([
      {
        url: '/api/v1/households/h1/shopping-list/recipes',
        body: { recipeId: 'r1', servings: 2, exceptIngredientIds: ['milk'] }
      }
    ]);
  });

  it('shops for the planned meal when one is coming and not shopped yet', async () => {
    vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-10-17T12:00:00') });
    plannedDays = [
      {
        date: '2026-10-17',
        meals: [
          {
            entryId: 'e1',
            recipeId: 'r1',
            title: 'Pancakes',
            slot: 'dinner',
            servings: 4,
            recipeServings: 2,
            isOnShoppingList: false
          }
        ]
      }
    ];

    open();

    expect(await screen.findByText(/Planned for Saturday, Dinner/)).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('checkbox', { name: /Milk/ })).not.toBeChecked());
    await userEvent.click(screen.getByRole('checkbox', { name: /Milk/ }));
    await userEvent.click(screen.getByRole('button', { name: 'Add 2 ingredients' }));

    expect(posted[0]).toEqual({
      url: '/api/v1/households/h1/shopping-list/meals',
      body: { from: '2026-10-17', entryId: 'e1' }
    });
  });
});
