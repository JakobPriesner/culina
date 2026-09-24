import { describe, expect, it } from 'vitest';

import { highlightMatches } from './highlight';

const marked = (text: string, query: string) =>
  highlightMatches(text, query)
    .filter((stretch) => stretch.matched)
    .map((stretch) => stretch.text);

describe('marking what a search matched', () => {
  it('marks the typed word inside a longer one, which is how German compounds are found', () => {
    expect(marked('Tomatensuppe mit Basilikum', 'tomate')).toEqual(['Tomate']);
  });

  it('ignores case and accents, as the search itself does', () => {
    expect(marked('Crème brûlée', 'creme brulee')).toEqual(['Crème', 'brûlée']);
  });

  it('marks every word of the query, wherever it is', () => {
    expect(marked('Reis mit Tomaten und Reisnudeln', 'reis tomaten')).toEqual([
      'Reis',
      'Tomaten',
      'Reis'
    ]);
  });

  it('leaves text alone that the query does not touch', () => {
    expect(highlightMatches('Waffeln', 'suppe')).toEqual([{ text: 'Waffeln', matched: false }]);
  });

  it('does not light up single letters', () => {
    expect(marked('Apfel', 'a')).toEqual([]);
  });

  it('keeps every character, marked or not, in order', () => {
    const stretches = highlightMatches('Ofengemüse mit Feta', 'gemuse feta');

    expect(stretches.map((stretch) => stretch.text).join('')).toBe('Ofengemüse mit Feta');
  });
});
