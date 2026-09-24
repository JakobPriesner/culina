import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { Interpretation, SearchChip } from '../types';
import SearchChips from './SearchChips.svelte';
import SearchNotice from './SearchNotice.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * Every way a search recovers from an empty answer is said, with the way back.
 * These hold each one to having a control, because a notice without one is a
 * search box that changed the question and only mentioned it.
 */
const vegetarian: SearchChip = {
  kind: 'diet',
  value: 'vegetarian',
  text: 'vegetarisch',
  start: 0,
  end: 11,
  word: null
};
const salmon: SearchChip = {
  kind: 'ingredient',
  value: 'salmon',
  text: 'mit Lachs',
  start: 12,
  end: 21,
  word: 'Lachs'
};

const reading = (overrides: Partial<Interpretation>): Interpretation => ({
  freeText: '',
  chips: [],
  correctedFrom: null,
  relaxed: [],
  conflict: [],
  ...overrides
});

const handlers = () => ({ onastyped: vi.fn(), onremove: vi.fn() });

describe('the search notices', () => {
  it('offers the words as typed after a correction', async () => {
    const on = handlers();
    renderWithProviders(SearchNotice, {
      props: {
        interpretation: reading({ freeText: 'Kokosmilch', correctedFrom: 'Kokosmlich' }),
        total: 1,
        query: 'Kokosmlich',
        ...on
      }
    });

    expect(screen.getByText(/Showing results for “Kokosmilch”/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Search for “Kokosmlich” instead' }));

    expect(on.onastyped).toHaveBeenCalledOnce();
  });

  it('names a contradiction and lets either half go', async () => {
    const on = handlers();
    renderWithProviders(SearchNotice, {
      props: {
        interpretation: reading({ chips: [vegetarian, salmon], conflict: [vegetarian, salmon] }),
        total: 0,
        query: 'vegetarisch mit Lachs',
        ...on
      }
    });

    expect(
      screen.getByText('“Vegetarian” and “With Lachs” rule each other out.')
    ).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Remove “With Lachs”' }));

    expect(on.onremove).toHaveBeenCalledWith(salmon);
    // A contradiction is not an absence: no offer to write the recipe.
    expect(screen.queryByRole('link', { name: 'New recipe' })).not.toBeInTheDocument();
  });

  it('turns nothing at all into the start of a task, where the page has no empty state of its own', () => {
    renderWithProviders(SearchNotice, {
      props: {
        interpretation: reading({ freeText: 'Schnitzel' }),
        total: 0,
        query: 'Schnitzel',
        ...handlers()
      }
    });

    expect(screen.getByText('No recipe for “Schnitzel”')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'New recipe' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Import' })).toBeInTheDocument();
  });

  it('leaves the offer to a page that has one', () => {
    renderWithProviders(SearchNotice, {
      props: {
        interpretation: reading({}),
        total: 0,
        query: 'Schnitzel',
        offer: false,
        ...handlers()
      }
    });

    expect(screen.queryByText('No recipe for “Schnitzel”')).not.toBeInTheDocument();
  });
});

describe('the chips', () => {
  it('removes a reading by its own name', async () => {
    const onremove = vi.fn();
    renderWithProviders(SearchChips, { props: { chips: [vegetarian, salmon], onremove } });

    expect(screen.getByRole('list', { name: 'Understood as' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Remove “Vegetarian”' }));

    expect(onremove).toHaveBeenCalledWith(vegetarian);
  });

  it('claims nothing when nothing was inferred', () => {
    renderWithProviders(SearchChips, { props: { chips: [], onremove: vi.fn() } });

    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });
});
