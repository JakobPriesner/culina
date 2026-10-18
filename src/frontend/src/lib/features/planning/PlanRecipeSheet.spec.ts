import { screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '$lib/test/render';
import { shopping } from '$features/shopping/stores/shopping.svelte';

import { mealPlan, type PlannedMeal } from './mealPlan.svelte';
import PlanRecipeSheet from './PlanRecipeSheet.svelte';
import { upcoming } from './upcoming.svelte';

/* Planning from a recipe: this kitchen's usual servings, and the rest of a bigger batch on the plan as leftovers. */
const meal = (entryId: string, servings: number, over: Partial<PlannedMeal> = {}): PlannedMeal =>
  ({
    entryId,
    recipeId: `r-${entryId}`,
    title: entryId,
    servings,
    recipeServings: 4,
    slot: 'dinner',
    isOnShoppingList: false,
    ...over
  }) as PlannedMeal;

let posted: Record<string, unknown>[] = [];

const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

beforeEach(() => {
  posted = [];
  vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-10-14T12:00:00') });
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

      if (input.method === 'POST' && url.pathname.endsWith('/meal-plan')) {
        const body = await input.json();
        posted.push(body);

        return json({
          from: '2026-10-12',
          days: [
            {
              date: body.date,
              meals: [meal('new', body.servings, { recipeId: 'stew', slot: body.slot })]
            }
          ]
        });
      }
      if (url.pathname.endsWith('/meal-plan')) {
        const thisWeek = url.searchParams.get('from') === '2026-10-12';

        return json({
          from: url.searchParams.get('from'),
          days: thisWeek
            ? [
                { date: '2026-10-14', meals: [meal('a', 2), meal('b', 2)] },
                { date: '2026-10-15', meals: [] },
                { date: '2026-10-16', meals: [] }
              ]
            : []
        });
      }

      return json({ items: [], version: 1 });
    })
  );
});

afterEach(() => {
  upcoming.reset();
  mealPlan.reset();
  shopping.reset();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

const open = () =>
  renderWithProviders(PlanRecipeSheet, {
    props: {
      open: true,
      householdId: 'h1',
      recipeId: 'stew',
      title: 'Stew',
      servings: 4,
      recipeServings: 4,
      onclose: vi.fn()
    }
  });

describe('planning a recipe for the week', () => {
  it('starts on the servings this kitchen usually plans, and says so', async () => {
    open();

    await waitFor(() =>
      expect(screen.getByRole('spinbutton', { name: 'Servings' })).toHaveValue(2)
    );
    expect(screen.getByText(/As usual in your kitchen: 2 servings/)).toBeInTheDocument();
  });

  it('plans the rest of the batch as leftovers, cooked and bought with the meal', async () => {
    open();

    await userEvent.click(
      await screen.findByRole('checkbox', { name: /Plan the rest for later: 2 servings/ })
    );
    await userEvent.click(screen.getByRole('button', { name: /add to week|zur woche/i }));

    await waitFor(() => expect(posted).toHaveLength(2));
    expect(posted[0]).toMatchObject({ recipeId: 'stew', servings: 4, slot: 'dinner' });
    expect(posted[1]).toMatchObject({
      recipeId: 'stew',
      servings: 2,
      slot: 'lunch',
      leftoverOf: 'new'
    });
  });
});
