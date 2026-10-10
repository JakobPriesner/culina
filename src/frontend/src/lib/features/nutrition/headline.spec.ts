import { beforeEach, describe, expect, it } from 'vitest';

import { metaFigureOf, missingNames, withoutWords } from './headline';
import type { Nutrition } from './types';
import { preferences } from '$shell/preferences.svelte';

const value = (amount: number, atLeast: boolean) => ({ value: amount, atLeast });

const plain = (text: string | null) => text?.replace(/\u00a0/g, ' ') ?? null;

const answer = (over: Partial<Nutrition>): Nutrition =>
  ({
    per: 'serving',
    yield: 2,
    complete: false,
    counted: 3,
    lines: 5,
    values: { energyKcal: value(520.9, true), energyKj: value(2179, true) },
    ingredients: [],
    ...over
  }) as unknown as Nutrition;

beforeEach(() => preferences.setLocale('en'));

describe('the short figure for the meta line', () => {
  it('is the figure with its qualifier', () => {
    expect(plain(metaFigureOf(answer({})))).toBe('at least 520 kcal');
    expect(
      plain(metaFigureOf(answer({ values: { energyKcal: value(520.4, false) } as never })))
    ).toBe('520 kcal');
  });

  it('says whole recipe for one portion', () => {
    expect(
      metaFigureOf(answer({ yield: 1, values: { energyKcal: value(6714, false) } as never }))
    ).toBe('whole recipe: 6,714 kcal');
  });

  it('is nothing while unknown or when nothing was counted', () => {
    expect(metaFigureOf(null)).toBeNull();
    expect(metaFigureOf(answer({ counted: 0 }))).toBeNull();
  });
});

describe('what is missing', () => {
  it('words zero, one, two and many names', () => {
    expect(withoutWords([])).toBeNull();
    expect(withoutWords(['onion'])).toBe('without onion');
    expect(withoutWords(['onion', 'salt'])).toBe('without onion and salt');
    expect(withoutWords(['onion', 'salt', 'oil'])).toBe('without onion, salt and 1 more');
    expect(withoutWords(['a', 'b', 'c', 'd'])).toBe('without a, b and 2 more');
  });

  it('names nothing for an exact energy', () => {
    const exact = answer({ values: { energyKcal: value(520, false) } as never });

    expect(missingNames(exact, { groups: [] } as never)).toEqual([]);
  });
});
