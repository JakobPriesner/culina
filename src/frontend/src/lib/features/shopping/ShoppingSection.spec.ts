import { render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it } from 'vitest';

import { preferences } from '$shell/preferences.svelte';

import ShoppingSection from './ShoppingSection.svelte';
import { togetherByName } from './lineRuns';
import type { ShoppingItem } from './stores/shopping.svelte';

/* Salt three times, each with a long note, read like three problems; it is one thing written three ways. */
const line = (itemId: string, name: string, over: Partial<ShoppingItem> = {}): ShoppingItem => ({
  itemId,
  name,
  quantity: null,
  unit: null,
  section: 'spices_baking',
  isChecked: false,
  isManual: false,
  sources: [],
  ...over
});

const salt = (itemId: string, unit: string | null) =>
  line(itemId, 'Salz', { quantity: unit ? 1 : null, unit, isListedSeparately: true });

afterEach(() => preferences.reset());

describe('lines of one thing that cannot be added up', () => {
  it('sit together, where the first of them is', () => {
    const items = [
      salt('a', 'pinch'),
      line('b', 'Zucker'),
      salt('c', 'tsp'),
      line('d', 'Mehl'),
      salt('e', null)
    ];

    expect(togetherByName(items).map((item) => item.itemId)).toEqual(['a', 'c', 'e', 'b', 'd']);
  });

  it('match their names whatever the case or spaces', () => {
    const items = [
      salt('a', 'pinch'),
      line('b', 'Zucker'),
      line('c', ' salz ', { isListedSeparately: true })
    ];

    expect(togetherByName(items).map((item) => item.itemId)).toEqual(['a', 'c', 'b']);
  });

  it('share one note, under the last of them', () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });

    render(ShoppingSection, {
      label: 'Spices',
      items: togetherByName([
        salt('a', 'pinch'),
        line('b', 'Zucker'),
        salt('c', 'tsp'),
        salt('d', null)
      ]),
      oncheck: () => {},
      onremove: () => {},
      onmove: () => {}
    });

    const notes = screen.getAllByText(/units that can't be added up/);
    expect(notes).toHaveLength(1);
    expect(notes[0]!.closest('li')).toHaveTextContent('Salz');
    expect(screen.getAllByRole('listitem').at(2)).toContainElement(notes[0]!);
  });
});
