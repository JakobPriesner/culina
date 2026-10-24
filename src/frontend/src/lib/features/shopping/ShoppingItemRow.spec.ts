import { fireEvent, render, screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { tick } from 'svelte';

import { provenance } from '$features/planning/provenance.svelte';
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

    await userEvent.click(screen.getByRole('button', { name: 'Actions for Milch' }));
    await fireEvent.click(screen.getByRole('button', { name: '2 recipes', hidden: true }));

    expect(screen.getByRole('link', { name: 'Waffles' })).toHaveAttribute('href', '/recipes/r1');
    expect(screen.getByRole('link', { name: 'Custard' })).toHaveAttribute('href', '/recipes/r2');
    expect(screen.getByText(/Sat, Sep 26 · Breakfast/)).toBeInTheDocument();
    expect(screen.getByText(/^300\sml$/)).toBeInTheDocument();
  });

  it('reads the sum as something to put in a basket, not as the exact total', () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });

    row({ ...milk, name: 'Butter', quantity: 1108.33, unit: 'g' });

    expect(screen.getByText(/^1\.1\skg$/)).toBeInTheDocument();
  });

  it('accounts for the part of the total no recipe asked for, so the parts reach the whole', async () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });

    row({
      ...milk,
      name: 'Butter',
      quantity: 700,
      unit: 'g',
      isManual: true,
      otherQuantity: 500,
      sources: [
        {
          recipeId: 'r1',
          recipeTitle: 'Waffles',
          quantity: 200,
          unit: 'g',
          plannedDate: null,
          plannedSlot: null
        }
      ]
    });

    await userEvent.click(screen.getByRole('button', { name: 'Actions for Butter' }));
    await fireEvent.click(screen.getByRole('button', { name: '1 recipe', hidden: true }));

    expect(screen.getByText('Added by hand or from elsewhere')).toBeInTheDocument();
    expect(screen.getByText(/^500\sg$/)).toBeInTheDocument();
  });

  it('says why one thing is on the list twice, when its units cannot be added', () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });

    row({ ...milk, name: 'Salt', quantity: 1, unit: 'tsp', isListedSeparately: true });

    expect(screen.getByText(/units that can't be added up/)).toBeInTheDocument();
  });

  it('says nothing of the kind once the line is in the trolley', () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });

    row({ ...milk, isChecked: true, isListedSeparately: true });

    expect(screen.queryByText(/can't be added up/)).not.toBeInTheDocument();
  });

  it('moves a line to the section it was chosen for, and only when it changes', async () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });

    const onmove = vi.fn();

    render(ShoppingItemRow, { item: milk, oncheck: () => {}, onremove: () => {}, onmove });

    await userEvent.click(screen.getByRole('button', { name: 'Actions for Milch' }));

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

    expect(screen.queryByRole('button', { name: 'Frozen', hidden: true })).not.toBeInTheDocument();
  });

  it('changes how much to buy, in the line’s own unit', async () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });
    const onquantity = vi.fn();

    render(ShoppingItemRow, {
      item: milk,
      oncheck: () => {},
      onremove: () => {},
      onmove: () => {},
      onquantity
    });

    await fireEvent.click(screen.getByRole('button', { name: 'Change the amount', hidden: true }));

    const field = screen.getByRole('spinbutton', { name: 'How much Milch' });

    await userEvent.clear(field);
    await userEvent.type(field, '750');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onquantity).toHaveBeenCalledWith(750);
  });

  it('asks whether a line is the same thing as another one, and merges on a tap', async () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });
    const onmerge = vi.fn();

    render(ShoppingItemRow, {
      item: milk,
      oncheck: () => {},
      onremove: () => {},
      onmove: () => {},
      sameAs: 'Vollmilch',
      onmerge
    });

    expect(screen.getByText(/Same as “Vollmilch”\?/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Merge' }));

    expect(onmerge).toHaveBeenCalledOnce();
  });

  it("says the share of the meal pointed at in the week beside, where several meals' amounts are merged", async () => {
    const source = { recipeId: 'r', recipeTitle: 'Dal', unit: 'ml', plannedDate: '2026-10-20' };
    row({
      ...milk,
      sources: [
        { ...source, quantity: 200, planEntryId: 'e1' },
        { ...source, recipeTitle: 'Curry', quantity: 300, planEntryId: 'e2' }
      ]
    });

    provenance.pointMeal('e1');
    await tick();
    expect(screen.getByText(/^200\sml (von|of) 500\sml$/)).toBeInTheDocument();

    provenance.pointMeal(null);
    await tick();
    expect(screen.getByText(/^500\sml$/)).toBeInTheDocument();
  });
});
