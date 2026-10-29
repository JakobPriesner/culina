import { screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import { renderWithProviders } from '#lib/test/render.js';

import SuggestionList from './SuggestionList.svelte';

/* The list marks what was typed the way search does: accents and umlaut spellings folded, every match marked. */
const render = (query: string, label: string) =>
  renderWithProviders(SuggestionList, {
    props: {
      id: 'list',
      label: 'Suggestions',
      items: [{ value: 'x', label }],
      highlighted: 0,
      onchoose: () => {},
      query
    }
  });

describe('the suggestion list', () => {
  it('marks an accented label for an unaccented query', () => {
    render('creme', 'Crème fraîche');

    expect(screen.getByRole('option').querySelector('mark')).toHaveTextContent('Crème');
  });

  it('marks nothing without a match', () => {
    render('zucker', 'Mehl');

    expect(screen.getByRole('option').querySelector('mark')).toBeNull();
  });
});
