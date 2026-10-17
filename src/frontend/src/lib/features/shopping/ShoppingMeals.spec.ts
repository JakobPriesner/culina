import { render, screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { preferences } from '$shell/preferences.svelte';

import type { ListedDay } from './plannedMeals';
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
        lines: [{ itemId: 'i1', name: 'Flour', quantity: 300, unit: 'g' }]
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
        lines: [{ itemId: 'i2', name: 'Yeast', quantity: null, unit: null }]
      }
    ]
  }
];

describe('the planned meals on the shopping list', () => {
  afterEach(() => preferences.reset());

  it('says how many meals the list is made of and how many are already past, before it is opened', () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });

    render(ShoppingMeals, { days });

    expect(screen.getByText(/Planned meals on this list: 2/)).toBeInTheDocument();
    expect(screen.getByText(/1 already past/)).toBeInTheDocument();
  });

  it('lets a past day be reviewed meal by meal, and says its lines stay', async () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });

    render(ShoppingMeals, { days });
    await userEvent.click(screen.getByText(/Planned meals on this list/));

    expect(screen.getByRole('link', { name: 'Waffles' })).toBeVisible();
    expect(screen.getByText('Flour')).toBeVisible();
    expect(screen.getByText(/stays on the list until you tick it off or remove it/)).toBeVisible();
    // Only the day that has passed is named so.
    expect(screen.getAllByText('Already past')).toHaveLength(1);
  });

  it('takes a past meal off the list in one go, and offers it for past meals only', async () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });
    const onwithdraw = vi.fn();

    render(ShoppingMeals, { days, onwithdraw });
    await userEvent.click(screen.getByText(/Planned meals on this list/));

    expect(screen.queryByRole('button', { name: /Pizza/ })).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Take off the list: Waffles' }));

    expect(onwithdraw).toHaveBeenCalledWith('e1', 'Waffles');
  });

  it('shows nothing for a list with no planned meals', () => {
    render(ShoppingMeals, { days: [] });

    expect(screen.queryByText(/Planned meals on this list|Geplante Mahlzeiten/)).toBeNull();
  });
});
