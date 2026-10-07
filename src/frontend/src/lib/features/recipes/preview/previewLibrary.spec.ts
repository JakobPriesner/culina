import { describe, expect, it } from 'vitest';

import { createPreviewLibrary } from './previewLibrary.svelte';
import type { PreviewRecipe } from './recipes';

const recipe = (id: string, minutes: number, ingredient = 'salt'): PreviewRecipe => ({
  id,
  title: `Recipe ${id}`,
  description: '',
  minutes,
  tag: 'Weeknight',
  ingredients: [{ name: ingredient, quantity: 1, unit: '' }],
  steps: []
});

const recipes = [recipe('orzo', 25), recipe('toast', 15, 'tomatoes'), recipe('rice', 40)];

const ids = (library: ReturnType<typeof createPreviewLibrary>) =>
  library.shown.map((one) => one.id);

describe('the preview library', () => {
  it('shows everything until something narrows it', () => {
    const library = createPreviewLibrary(recipes);

    expect(ids(library)).toEqual(['orzo', 'toast', 'rice']);
    expect(library.narrowed).toBe(false);
  });

  it('searches the title, the tag and the ingredients', () => {
    const library = createPreviewLibrary(recipes);

    library.search = ' TOMATOES ';

    expect(ids(library)).toEqual(['toast']);
    expect(library.narrowed).toBe(true);
  });

  it('keeps the quick ones, up to thirty minutes', () => {
    const library = createPreviewLibrary(recipes);

    library.filter = 'quick';

    expect(ids(library)).toEqual(['orzo', 'toast']);
  });

  it('keeps the favourites, which start with the orzo and follow the toggle', () => {
    const library = createPreviewLibrary(recipes);

    library.filter = 'favourites';
    library.toggleFavourite('rice');
    expect(ids(library)).toEqual(['orzo', 'rice']);

    library.toggleFavourite('orzo');
    expect(ids(library)).toEqual(['rice']);
  });

  it('clears the search and the filter together', () => {
    const library = createPreviewLibrary(recipes);
    library.search = 'nothing matches this';
    library.filter = 'quick';

    library.reset();

    expect(library.shown).toHaveLength(3);
    expect(library.narrowed).toBe(false);
  });
});
