import { describe, expect, it } from 'vitest';

import { wordYield, yieldNoun } from './yieldWords';

describe('what a recipe makes, in words', () => {
  it('words the kind when the recipe has no word of its own', () => {
    expect(wordYield(4, { yieldKind: 'servings', yieldLabel: null })).toBe('4 servings');
    expect(wordYield(12, { yieldKind: 'pieces', yieldLabel: null })).toBe('12 pieces');
  });

  it('takes the recipe at its own word', () => {
    expect(wordYield(1, { yieldKind: 'servings', yieldLabel: 'Cake' })).toBe('1 Cake');
  });

  it('never pluralises that word, because it cannot know the plural', () => {
    // "2 Blech" reads as somebody's shorthand. "2 Blechs" reads as a bug.
    expect(wordYield(2, { yieldKind: 'servings', yieldLabel: 'Blech' })).toBe('2 Blech');
  });

  it('gives the stepper the noun alone, so it can print the number itself', () => {
    expect(yieldNoun({ yieldKind: 'servings', yieldLabel: null })).toBe('Servings');
    expect(yieldNoun({ yieldKind: 'pieces', yieldLabel: null })).toBe('Pieces');
    expect(yieldNoun({ yieldKind: 'pieces', yieldLabel: 'Gläser' })).toBe('Gläser');
  });
});
