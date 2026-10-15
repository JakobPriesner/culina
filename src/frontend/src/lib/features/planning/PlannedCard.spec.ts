import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '$lib/test/render';

import type { PlannedMeal } from './mealPlan.svelte';
import PlannedCard from './PlannedCard.svelte';

/* One planned meal: a single control edits it, so keyboard and screen-reader users meet one stop, not two. */
const meal = {
  entryId: 'e1',
  recipeId: 'r1',
  title: 'Tims Chili',
  slot: 'dinner',
  servings: 4,
  recipeServings: 4,
  isOnShoppingList: false
} as PlannedMeal;

describe('a planned meal card', () => {
  it('has one button that opens the meal, besides removing it', async () => {
    const onedit = vi.fn();

    renderWithProviders(PlannedCard, { props: { meal, onremove: () => {}, onedit } });

    const buttons = screen.getAllByRole('button');

    expect(buttons).toHaveLength(2);

    await userEvent.click(screen.getByRole('button', { name: /Edit or move Tims Chili/ }));

    expect(onedit).toHaveBeenCalledOnce();
  });
});
