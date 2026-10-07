import { fireEvent, render, screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { preferences } from '$shell/preferences.svelte';

import ShoppingItemRow from './ShoppingItemRow.svelte';
import type { ShoppingItem } from './stores/shopping.svelte';

const milk: ShoppingItem = {
  itemId: 'i1',
  name: 'Milch',
  quantity: 500,
  unit: 'ml',
  section: 'dairy_eggs',
  isChecked: false,
  isManual: false,
  sources: []
};

const row = (item: ShoppingItem) =>
  render(ShoppingItemRow, { item, oncheck: () => {}, onremove: () => {}, onmove: () => {} });

describe('a line on the shopping list', () => {
  afterEach(() => preferences.reset());

  it('says its amount in the units the recipe said it in, by default', () => {
    row(milk);

    expect(screen.getByText(/^500\sml$/)).toBeInTheDocument();
  });

  it('says it in cups for an imperial kitchen, as the recipe beside it does', () => {
    // The recipe reads "~2 cups" under this setting; the list must not switch back to millilitres.
    preferences.adopt({ measurementSystem: 'imperial' }, { signedIn: false });

    row(milk);

    expect(screen.getByText(/^~2\scups$/)).toBeInTheDocument();
  });

  it('reveals which recipes contributed a merged line only when asked', async () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });

    row({
      ...milk,
      sources: [
        {
          recipeId: 'r1',
          recipeTitle: 'Waffles',
          quantity: 300,
          unit: 'ml',
          plannedDate: '2026-09-26',
          plannedSlot: 'breakfast'
        },
        {
          recipeId: 'r2',
          recipeTitle: 'Custard',
          quantity: 200,
          unit: 'ml',
          plannedDate: null,
          plannedSlot: null
        }
      ]
    });

    expect(screen.queryByRole('link', { name: 'Waffles' })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: '2 recipes' }));

    expect(screen.getByRole('link', { name: 'Waffles' })).toHaveAttribute('href', '/recipes/r1');
    expect(screen.getByRole('link', { name: 'Custard' })).toHaveAttribute('href', '/recipes/r2');
    expect(screen.getByText(/Sat, Sep 26 · Breakfast/)).toBeInTheDocument();
    expect(screen.getByText(/^300\sml$/)).toBeInTheDocument();
  });

  it('moves a line to the section it was chosen for, and only when it changes', async () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });

    const onmove = vi.fn();

    render(ShoppingItemRow, { item: milk, oncheck: () => {}, onremove: () => {}, onmove });

    await userEvent.click(screen.getByRole('button', { name: 'Move Milch to another section' }));

    // jsdom has no popover, so the panel never opens; its contents are still pressable.
    const current = screen.getByRole('button', { name: 'Dairy & eggs', hidden: true });

    expect(current).toHaveAttribute('aria-current', 'true');

    await fireEvent.click(current);
    expect(onmove).not.toHaveBeenCalled();

    await fireEvent.click(screen.getByRole('button', { name: 'Frozen', hidden: true }));
    expect(onmove).toHaveBeenCalledWith('frozen');
  });

  it('offers no move once the line is in the trolley', () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });

    row({ ...milk, isChecked: true });

    expect(
      screen.queryByRole('button', { name: 'Move Milch to another section' })
    ).not.toBeInTheDocument();
  });
});
