import { afterEach, describe, expect, it, vi } from 'vitest';

import { createLingering } from './lingering.svelte';
import { groupsToBuy } from './lineRuns';
import type { ShoppingItem } from './stores/shopping.svelte';

/* A ticked line leaving at once moves the next one under the thumb; it waits a moment, struck through. */
const line = (itemId: string, isChecked = false): ShoppingItem => ({
  itemId,
  name: itemId,
  quantity: null,
  unit: null,
  section: 'produce',
  isChecked,
  isManual: false,
  sources: []
});

afterEach(() => vi.useRealTimers());

describe('a line just ticked', () => {
  it('stays where it was while it is held, and leaves after', () => {
    const items = [line('Limetten', true), line('Äpfel')];

    expect(groupsToBuy(items, ['Limetten'])[0]!.items.map((item) => item.itemId)).toEqual([
      'Limetten',
      'Äpfel'
    ]);
    expect(groupsToBuy(items)[0]!.items.map((item) => item.itemId)).toEqual(['Äpfel']);
  });

  it('is held for a moment only', () => {
    vi.useFakeTimers();
    const lingering = createLingering(1200);

    lingering.hold('Limetten');
    expect(lingering.ids).toEqual(['Limetten']);

    vi.advanceTimersByTime(1199);
    expect(lingering.ids).toEqual(['Limetten']);

    vi.advanceTimersByTime(1);
    expect(lingering.ids).toEqual([]);
  });
});
