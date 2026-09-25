import { describe, expect, it } from 'vitest';

import type { Interpretation, SearchChip } from '../types';
import { shelfFrom, shelvable } from './shelf';

const chip = (
  kind: SearchChip['kind'],
  value: string,
  text: string,
  word: string | null = null
) => ({
  kind,
  value,
  text,
  start: 0,
  end: text.length,
  word
});

const read = (chips: SearchChip[], freeText = ''): Interpretation => ({
  freeText,
  chips,
  correctedFrom: null,
  relaxed: [],
  conflict: []
});

describe('a search, as a cookbook', () => {
  it('carries a time limit, the ingredients and the tags across exactly', () => {
    const shelving = shelfFrom(
      read([
        chip('time', '30', 'unter 30 Minuten'),
        chip('ingredient', 'potato', 'mit Kartoffeln', 'Kartoffeln')
      ]),
      [{ slug: 'ofengericht' }]
    );

    // The same question, asked by a shelf: nothing added and nothing lost.
    expect(shelving.rules).toEqual({
      tags: ['ofengericht'],
      ingredients: ['Kartoffeln'],
      maxMinutes: 30
    });
    expect(shelving.behind).toEqual([]);
    expect(shelvable(shelving)).toBe(true);
  });

  it('names what a shelf cannot ask, rather than dropping it', () => {
    const shelving = shelfFrom(
      read(
        [chip('diet', 'vegetarian', 'vegetarisch'), chip('time', '20', 'unter 20 Minuten')],
        'Auflauf'
      ),
      []
    );

    expect(shelving.rules.maxMinutes).toBe(20);
    expect(shelving.behind).toEqual(['vegetarisch', 'Auflauf']);
  });

  it('has nothing to offer for a search that is only words', () => {
    const shelving = shelfFrom(read([chip('cuisine', 'italian', 'italienisch')], 'Pasta'), []);

    // A cookbook of every recipe would not be what anybody asked for.
    expect(shelvable(shelving)).toBe(false);
  });

  it('keeps the shorter of two time limits', () => {
    const shelving = shelfFrom(
      read([chip('time', '45', 'unter 45 Minuten'), chip('time', '30', 'in 30 Minuten')]),
      []
    );

    expect(shelving.rules.maxMinutes).toBe(30);
  });
});
