import { describe, expect, it } from 'vitest';

import { groupsToBuy } from './lineRuns';
import type { ShoppingItem } from './stores/shopping.svelte';

const line = (itemId: string, section: string, isChecked = false): ShoppingItem =>
  ({
    itemId,
    name: itemId,
    section,
    isChecked,
    isManual: true,
    sources: []
  }) as unknown as ShoppingItem;

describe('what is still to buy', () => {
  it('goes by section in shop order, without the ticked lines', () => {
    const groups = groupsToBuy([
      line('Milch', 'dairy_eggs'),
      line('Äpfel', 'produce'),
      line('Brot', 'bakery', true)
    ]);

    expect(groups.map((group) => group.section)).toEqual(['produce', 'dairy_eggs']);
  });

  it('draws a section this client does not know as "other", so every counted line is shown', () => {
    const groups = groupsToBuy([line('Tofu', 'plant_based'), line('Äpfel', 'produce')]);

    expect(groups).toEqual([
      { section: 'produce', items: [expect.objectContaining({ itemId: 'Äpfel' })] },
      { section: 'other', items: [expect.objectContaining({ itemId: 'Tofu' })] }
    ]);
  });
});
