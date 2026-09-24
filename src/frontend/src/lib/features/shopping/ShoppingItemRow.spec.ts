import { render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it } from 'vitest';

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
  isManual: false
};

const row = (item: ShoppingItem) =>
  render(ShoppingItemRow, { item, oncheck: () => {}, onremove: () => {} });

describe('a line on the shopping list', () => {
  afterEach(() => preferences.reset());

  it('says its amount in the units the recipe said it in, by default', () => {
    row(milk);

    expect(screen.getByText(/^500\sml$/)).toBeInTheDocument();
  });

  it('says it in cups for an imperial kitchen, as the recipe beside it does', () => {
    // The recipe it came from reads "~2 cups" under this setting; the list the
    // same milk lands on must not switch back to millilitres.
    preferences.adopt({ measurementSystem: 'imperial' }, { signedIn: false });

    row(milk);

    expect(screen.getByText(/^~2\scups$/)).toBeInTheDocument();
  });
});
