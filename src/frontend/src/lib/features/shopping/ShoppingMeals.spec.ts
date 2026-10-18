import { render, screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { preferences } from '$shell/preferences.svelte';

import type { ListedDay } from './listedMeals';
import ShoppingMeals from './ShoppingMeals.svelte';

const days: ListedDay[] = [
  {
    date: '2026-09-23',
    isPast: true,
    meals: [
      {
        planEntryId: 'e1',
        recipeId: 'r1',
        recipeTitle: 'Waffles',
        slot: 'breakfast',
        lines: [{ itemId: 'i1', name: 'Flour', quantity: 300, unit: 'g', checked: false }],
        done: 0
      }
    ]
  },
  {
    date: '2026-10-20',
    isPast: false,
    meals: [
      {
        planEntryId: 'e2',
        recipeId: 'r2',
        recipeTitle: 'Pizza',
        slot: 'dinner',
        lines: [
          { itemId: 'i2', name: 'Yeast', quantity: null, unit: null, checked: false },
          { itemId: 'i3', name: 'Flour', quantity: 500, unit: 'g', checked: true }
        ],
        done: 1
      }
    ]
  },
  {
    date: null,
    isPast: false,
    meals: [
      {
        planEntryId: null,
        recipeId: 'r3',
        recipeTitle: 'Bread',
        slot: null,
        lines: [{ itemId: 'i4', name: 'Salt', quantity: null, unit: null, checked: false }],
        done: 0
      }
    ]
  }
];

describe('the planned meals on the shopping list', () => {
  afterEach(() => preferences.reset());

  it('says how many meals the list is made of and how many are already past, before it is opened', () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });

    render(ShoppingMeals, { days });

    expect(screen.getByText(/Meals and recipes on this list: 3/)).toBeInTheDocument();
    expect(screen.getByText(/1 already past/)).toBeInTheDocument();
  });

  it('lets a past day be reviewed meal by meal, and says its lines stay', async () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });

    render(ShoppingMeals, { days });
    await userEvent.click(screen.getByText(/Meals and recipes on this list/));

    expect(screen.getByRole('link', { name: 'Waffles' })).toBeVisible();
    expect(screen.getAllByText('Flour')[0]).toBeVisible();
    expect(screen.getByText(/stays on the list until you tick it off or remove it/)).toBeVisible();
    // Only the day that has passed is named so.
    expect(screen.getAllByText('Already past')).toHaveLength(1);
  });

  it('takes any meal or recipe off the list in one go, past or still to come', async () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });
    const onwithdraw = vi.fn();

    render(ShoppingMeals, { days, onwithdraw });
    await userEvent.click(screen.getByText(/Meals and recipes on this list/));

    await userEvent.click(screen.getByRole('button', { name: 'Take off the list: Pizza' }));
    await userEvent.click(screen.getByRole('button', { name: 'Take off the list: Bread' }));

    expect(onwithdraw.mock.calls.map(([meal]) => meal.recipeTitle)).toEqual(['Pizza', 'Bread']);
  });

  it('shows how far a meal is along, keeping its bought lines in sight', async () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });

    render(ShoppingMeals, { days });
    await userEvent.click(screen.getByText(/Meals and recipes on this list/));

    expect(screen.getByText('1 of 2 done')).toBeVisible();
    expect(screen.getByText('Added without a day')).toBeVisible();
  });

  it('shows nothing for a list with no planned meals', () => {
    render(ShoppingMeals, { days: [] });

    expect(screen.queryByText(/on this list|auf dieser Liste/)).toBeNull();
  });
});
