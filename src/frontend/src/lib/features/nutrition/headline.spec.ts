import { beforeEach, describe, expect, it } from 'vitest';

import { coverageWords, metaFigureOf, missingNames, withoutWords } from './headline';
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
  it('keeps the visible figure simple and can qualify its accessible name', () => {
    expect(plain(metaFigureOf(answer({})))).toBe('520 kcal');
    expect(plain(metaFigureOf(answer({}), true))).toBe('at least 520 kcal');
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

describe('what counts, for the editor', () => {
  const recipe = {
    groups: [
      {
        ingredients: ['butter', 'onion', 'salt', 'oil', 'chives', 'pepper'].map((name) => ({
          id: name,
          name
        }))
      }
    ]
  } as never;

  const line = (ingredientId: string, status: string, reason?: string) => ({
    ingredientId,
    status,
    reason,
    canRaiseEnergy: true
  });

  const lines = (...rest: ReturnType<typeof line>[]) => rest as never;

  it('says every line counts', () => {
    expect(coverageWords(answer({ complete: true, counted: 6, lines: 6 }), recipe)).toBe(
      'Nutrition: every line counts'
    );
  });

  it('counts the lines and names what is left out, in the recipe order, short', () => {
    const words = coverageWords(
      answer({
        counted: 4,
        lines: 6,
        ingredients: lines(
          line('butter', 'counted'),
          line('onion', 'amountNotInGrams', 'count'),
          line('salt', 'noAmount'),
          line('oil', 'counted'),
          line('chives', 'counted'),
          line('pepper', 'counted')
        )
      }),
      recipe
    );

    expect(words).toBe('Nutrition: 4 of 6 lines count · onion: a count · salt: no amount');
  });

  it('names at most three, then how many more', () => {
    const words = coverageWords(
      answer({
        counted: 2,
        lines: 6,
        ingredients: lines(
          line('butter', 'counted'),
          line('onion', 'amountNotInGrams', 'count'),
          line('salt', 'noAmount'),
          line('oil', 'amountNotInGrams', 'spoonOfSolid'),
          line('chives', 'unknownFood'),
          line('pepper', 'counted')
        )
      }),
      recipe
    );

    expect(words).toBe(
      'Nutrition: 2 of 6 lines count · onion: a count · salt: no amount · oil: spoonful · and 1 more'
    );
  });

  it('is German too, and has nothing to say about a recipe with no lines', () => {
    preferences.setLocale('de');

    expect(coverageWords(answer({ complete: true, counted: 6, lines: 6 }), recipe)).toBe(
      'Nährwerte: alle Zeilen zählen'
    );
    expect(
      coverageWords(
        answer({ counted: 1, lines: 2, ingredients: lines(line('salt', 'noAmount')) }),
        recipe
      )
    ).toBe('Nährwerte: 1 von 2 Zeilen zählen · salt: keine Menge');
    expect(coverageWords(answer({ counted: 0, lines: 0 }), recipe)).toBeNull();
  });
});
