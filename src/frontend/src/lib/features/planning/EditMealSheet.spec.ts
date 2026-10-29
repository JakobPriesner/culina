import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '#lib/test/render.js';

import EditMealSheet from './EditMealSheet.svelte';
import type { PlannedDay, PlannedMeal } from './mealPlan.svelte';

/* Editing a planned meal: its servings are its own, and the shopping list is said to hold a sum, not silently changed. */
const chili = (over: Partial<PlannedMeal> = {}): PlannedMeal =>
  ({
    entryId: 'e1',
    recipeId: 'r1',
    title: 'Tims Chili',
    slot: 'dinner',
    servings: 8,
    recipeServings: 4,
    isOnShoppingList: false,
    ...over
  }) as PlannedMeal;

const week = (meal: PlannedMeal): PlannedDay[] => [
  { date: '2026-10-13', meals: [meal] },
  { date: '2026-10-14', meals: [] }
];

beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };
  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.open = false;
    this.dispatchEvent(new Event('close'));
  };
});

const render = (meal: PlannedMeal) => {
  const onsave = vi.fn();

  renderWithProviders(EditMealSheet, {
    props: { meal, days: week(meal), from: '2026-10-13', onsave, onclose: vi.fn() }
  });

  return onsave;
};

describe('the sheet for a planned meal', () => {
  it('opens on the servings the meal has, and says the recipe keeps its own', () => {
    render(chili());

    expect(screen.getByRole('spinbutton', { name: 'Servings' })).toHaveValue(8);
    expect(screen.getByText(/recipe itself stays written for 4 servings/)).toBeInTheDocument();
  });

  it("opens on the recipe's servings for a meal that never chose any", () => {
    render(chili({ servings: undefined }));

    expect(screen.getByRole('spinbutton', { name: 'Servings' })).toHaveValue(4);
  });

  it('saves the servings that were set, along with the day and meal', async () => {
    const onsave = render(chili());

    await userEvent.click(screen.getByRole('button', { name: 'One fewer' }));
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onsave).toHaveBeenCalledWith({
      date: '2026-10-13',
      slot: 'dinner',
      servings: 7,
      updateList: true
    });
  });

  it('keeps an odd figure as it was when nobody touched the servings', async () => {
    const onsave = render(chili({ servings: 7.4 }));

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onsave).toHaveBeenCalledWith(expect.objectContaining({ servings: 7.4 }));
  });

  it('says nothing of a shopping list when the meal is not on it', () => {
    render(chili());

    expect(screen.queryByText(/shopping list/)).not.toBeInTheDocument();
  });

  it('says what the list holds, and lets the change follow or not', async () => {
    const onsave = render(chili({ isOnShoppingList: true }));

    expect(
      screen.getByText('Its ingredients are on your shopping list for 8 servings.')
    ).toBeInTheDocument();

    await userEvent.click(screen.getByRole('checkbox', { name: 'Update the shopping list too' }));
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onsave).toHaveBeenCalledWith(expect.objectContaining({ updateList: false }));
  });
});
