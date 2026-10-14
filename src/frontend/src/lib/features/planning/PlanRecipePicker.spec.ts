import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '$lib/test/render';
import { recipes } from '$features/recipes/stores/recipes.svelte';
import { suggestions } from '$features/recipes/stores/suggestions.svelte';
import { preferences } from '$shell/preferences.svelte';

import type { PlannedDay, PlannedMeal } from './mealPlan.svelte';
import PlanRecipePicker from './PlanRecipePicker.svelte';

/* The sheet that opens over the week to pick a meal: it says which day it is for, and which recipes the week already holds. */
const summary = (id: string, title: string) => ({
  recipeId: id,
  title,
  imageId: null,
  totalMinutes: 25,
  yieldAmount: 4,
  yieldKind: 'servings',
  tags: [],
  cookCount: 0,
  updatedAt: '2026-09-12T00:00:00Z',
  ingredientMatch: null
});

const meal = (entryId: string, recipeId: string, slot = 'dinner'): PlannedMeal =>
  ({
    entryId,
    recipeId,
    title: recipeId,
    slot,
    recipeServings: 4,
    isOnShoppingList: false
  }) as PlannedMeal;

const day = (date: string, ...meals: PlannedMeal[]): PlannedDay => ({ date, meals });

beforeEach(() => {
  recipes.reset();
  suggestions.reset();
  preferences.adopt({ locale: 'en' }, { signedIn: false });

  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };
  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.open = false;
    this.dispatchEvent(new Event('close'));
  };

  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request) =>
      Promise.resolve(
        new Response(
          JSON.stringify(
            // Only a typed name asks for recipes: the blank state asks for suggestions, and the sheet for cookbooks.
            new URL(input.url).pathname.endsWith('/recipes')
              ? {
                  items: [summary('chili', 'Chili'), summary('dal', 'Dal')],
                  nextCursor: null,
                  total: 2
                }
              : { items: [] }
          ),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        )
      )
    )
  );
});

afterEach(() => {
  vi.unstubAllGlobals();
  preferences.reset();
});

const props = (over: { date: string | null; days: PlannedDay[] }) => ({
  open: true,
  householdId: 'h1',
  slot: 'dinner' as const,
  onpick: vi.fn(),
  onclose: vi.fn(),
  ...over
});

/** Searched by name: the blank state suggests meals and leaves the planned ones out. */
async function search(over: { date: string | null; days: PlannedDay[] }) {
  renderWithProviders(PlanRecipePicker, { props: props(over) });

  await userEvent.type(screen.getByRole('searchbox'), 'chi');

  return screen.findByRole('button', { name: /Chili/ });
}

describe('the picker for a planned day', () => {
  it('names the day it is for, so the sheet over the week still says which day this is', () => {
    renderWithProviders(PlanRecipePicker, { props: props({ date: '2026-10-14', days: [] }) });

    expect(screen.getByRole('dialog', { name: /Wednesday, October 14/ })).toBeInTheDocument();
  });

  it('says on which day and meal a recipe is already planned, instead of only "Added"', async () => {
    const chili = await search({
      date: '2026-10-14',
      days: [day('2026-10-13', meal('e1', 'chili'))]
    });

    expect(chili).toHaveTextContent('Planned Tue, Oct 13 · Dinner');
    expect(chili).not.toHaveTextContent('Added');
    expect(screen.getByRole('button', { name: /Dal/ })).not.toHaveTextContent('Planned');
  });

  it('keeps a planned recipe pressable, since a meal can be cooked twice in a week', async () => {
    const chili = await search({
      date: '2026-10-14',
      days: [day('2026-10-13', meal('e1', 'chili'))]
    });

    expect(chili).not.toHaveAttribute('aria-disabled');
    expect(chili).not.toBeDisabled();
  });

  it('counts the occurrences it does not list', async () => {
    const chili = await search({
      date: '2026-10-16',
      days: [
        day('2026-10-13', meal('e1', 'chili')),
        day('2026-10-14', meal('e2', 'chili', 'lunch')),
        day('2026-10-15', meal('e3', 'chili'))
      ]
    });

    expect(chili).toHaveTextContent('+ 1 more');
  });
});
