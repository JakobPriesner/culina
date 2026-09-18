import { beforeEach, describe, expect, it } from 'vitest';
import { resetAllStores } from '$shell/stores';

import {
  effectiveSort,
  fromWireSort,
  libraryView,
  RecipeQuery,
  sortsFor,
  toWireSort,
  type RecipeSort
} from './libraryView.svelte';

beforeEach(() => libraryView.reset());

describe('the library view', () => {
  it('keeps a household’s search and filters on return', () => {
    libraryView.forHousehold('one');
    libraryView.query = 'tomato';
    libraryView.quick = true;
    libraryView.toggleTag('vegetarisch');
    libraryView.sort = 'title';
    libraryView.forHousehold('one');
    expect(libraryView.query).toBe('tomato');
    expect(libraryView.quick).toBe(true);
    expect(libraryView.tags).toEqual(['vegetarisch']);
    expect(libraryView.sort).toBe('title');
  });

  it('does not carry another household’s filters across', () => {
    libraryView.forHousehold('one');
    libraryView.query = 'tomato';
    libraryView.quick = true;
    libraryView.toggleTag('vegetarisch');
    libraryView.forHousehold('two');
    expect(libraryView.query).toBe('');
    expect(libraryView.quick).toBe(false);
    // A tag slug is one kitchen's word. Carrying it across would filter by
    // something nobody in the new one uses, and look broken rather than empty.
    expect(libraryView.tags).toEqual([]);
  });

  it('forgets the search at sign-out', () => {
    libraryView.forHousehold('one');
    libraryView.query = 'tomato';
    libraryView.quick = true;
    resetAllStores();
    expect(libraryView.query).toBe('');
    expect(libraryView.quick).toBe(false);
  });
});

describe('what the library is being asked', () => {
  it('treats the quick chip as one value of the time filter', () => {
    // Two flags that could disagree were one bug waiting: "quick" on and the
    // ceiling cleared is a state nothing could render honestly.
    const view = new RecipeQuery();

    view.quick = true;
    expect(view.maxMinutes).toBe(30);

    view.maxMinutes = 45;
    expect(view.quick).toBe(false);
  });

  it('counts what the panel would show, and not the words', () => {
    const view = new RecipeQuery();

    view.query = 'auflauf';
    expect(view.activeCount).toBe(0);

    view.toggleTag('vegetarisch');
    view.maxMinutes = 30;
    view.sort = 'title';
    expect(view.activeCount).toBe(3);
  });

  it('takes a whole saved search at once, and hands it back the same', () => {
    const view = new RecipeQuery();
    const saved = {
      query: 'auflauf',
      tags: ['vegetarisch'],
      maxMinutes: 30,
      sort: 'quickest' as const
    };

    view.assign(saved);
    expect(view.snapshot()).toEqual(saved);
  });

  it('empties every dimension, not only the one that was last touched', () => {
    const view = new RecipeQuery();

    view.assign({ query: 'x', tags: ['y'], maxMinutes: 15, sort: 'title' });
    view.clear();

    expect(view.snapshot()).toEqual({ query: '', tags: [], maxMinutes: null, sort: null });
    expect(view.filtered).toBe(false);
  });
});

describe('which order a list is in', () => {
  it('ranks a question, and leaves a browse alone', () => {
    const browsing = { searching: false, ranks: false, inACookbook: false };

    expect(effectiveSort(null, { ...browsing, searching: true })).toBe('relevance');
    expect(effectiveSort(null, browsing)).toBe('recent');
    expect(effectiveSort(null, { ...browsing, ranks: true })).toBe('suggested');
    expect(effectiveSort(null, { ...browsing, inACookbook: true })).toBe('shelf');
  });

  it('lets a choice beat every default', () => {
    // Picking "recently updated" has to stick even on a day the ranking would
    // have offered to take over.
    expect(effectiveSort('recent', { searching: true, ranks: true, inACookbook: true })).toBe(
      'recent'
    );
  });

  it('offers an order only where it means something', () => {
    const browsing = { searching: false, ranks: false, inACookbook: false };

    expect(sortsFor(browsing)).not.toContain('relevance');
    expect(sortsFor(browsing)).not.toContain('suggested');
    expect(sortsFor(browsing)).not.toContain('shelf');
    expect(sortsFor({ searching: true, ranks: true, inACookbook: true })).toContain('relevance');
  });

  it('spells every order the way the API reads it', () => {
    // The bug this replaced: the union said `match`, and the API answered 400.
    // An integration test holds the endpoint to the other half of this.
    const every: readonly RecipeSort[] = [
      'relevance',
      'suggested',
      'recent',
      'title',
      'quickest',
      'mostCooked',
      'shelf'
    ];

    expect(every.map(toWireSort)).toEqual([
      'relevance',
      'suggested',
      '-updatedAt',
      'title',
      'totalMinutes',
      '-cookCount',
      'cookbookOrder'
    ]);
  });

  it('reads back everything a saved search can store', () => {
    expect(fromWireSort('totalMinutes')).toBe('quickest');
    expect(fromWireSort('-updatedAt')).toBe('recent');
    expect(fromWireSort(null)).toBeNull();
    // A shelf's own order needs a shelf to be an order of, so a saved search
    // that somehow carried one applies as no order at all rather than a 400.
    expect(fromWireSort('cookbookOrder')).toBeNull();
  });
});
