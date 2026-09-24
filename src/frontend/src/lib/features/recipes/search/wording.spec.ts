import { describe, expect, it } from 'vitest';

import type { SearchChip } from '../types';
import { chipLabel, reasonLine, withoutChip } from './wording';

const chip = (
  kind: SearchChip['kind'],
  value: string,
  query: string,
  text: string,
  word?: string
) => {
  const start = query.indexOf(text);

  return {
    kind,
    value,
    text,
    start,
    end: start + text.length,
    word: word ?? null
  } satisfies SearchChip;
};

describe('removing a chip', () => {
  it('is deleting its characters and nothing else', () => {
    const query = 'vegetarisch unter 30 Minuten mit Kartoffeln';

    expect(withoutChip(query, chip('time', '30', query, 'unter 30 Minuten'))).toBe(
      'vegetarisch mit Kartoffeln'
    );
    expect(withoutChip(query, chip('diet', 'vegetarian', query, 'vegetarisch'))).toBe(
      'unter 30 Minuten mit Kartoffeln'
    );
  });

  it('takes the whole sentence when the chip is the sentence', () => {
    const query = 'was kann ich mit Kartoffeln machen?';

    expect(
      withoutChip(query, chip('ingredient', 'potato', query, 'was kann ich mit Kartoffeln machen'))
    ).toBe('?');
  });
});

describe('wording a reading', () => {
  it('names what it understood, in words rather than keys', () => {
    const query = 'vegetarisch Abendessen ohne Zwiebeln unter 20 Minuten';

    expect(chipLabel(chip('diet', 'vegetarian', query, 'vegetarisch'))).toBe('Vegetarian');
    expect(chipLabel(chip('meal', 'dinner', query, 'Abendessen'))).toBe('Dinner');
    expect(chipLabel(chip('exclusion', 'onion', query, 'ohne Zwiebeln', 'Zwiebeln'))).toBe(
      'Without Zwiebeln'
    );
    expect(chipLabel(chip('time', '20', query, 'unter 20 Minuten'))).toBe('Under 20 min');
  });

  it('falls back to what was typed for a value it has no words for', () => {
    expect(chipLabel(chip('cuisine', 'martian', 'marsisch', 'marsisch'))).toBe('marsisch');
  });

  it('says why a result is there', () => {
    expect(reasonLine({ kind: 'ingredient', term: 'Kokosmilch' })).toBe('Ingredient: Kokosmilch');
    expect(reasonLine({ kind: 'concept', term: 'Dessert' })).toBe('Similar: Dessert');
    expect(reasonLine({ kind: 'text', term: null })).toBe('Mentioned in the method');
  });
});
