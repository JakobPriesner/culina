import { beforeEach, describe, expect, it } from 'vitest';

import {
  coverageWords,
  estimatedNames,
  estimatedWords,
  metaFigureOf,
  missingNames,
  withoutWords
} from './headline';
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
  it('says a floor is a floor, visibly and in its accessible name', () => {
    expect(plain(metaFigureOf(answer({})))).toBe('at least 520 kcal');
    expect(plain(metaFigureOf(answer({}), true))).toBe('at least 520 kcal');
    expect(
      plain(metaFigureOf(answer({ values: { energyKcal: value(520.4, false) } as never })))
    ).toBe('520 kcal');
  });

  it('says the figure is incomplete instead of a floor far below it', () => {
    const chili = answer({
      values: { energyKcal: { value: 184, atLeast: true, mostlyMissing: true } } as never
    });

    expect(metaFigureOf(chili)).toBe('calories incomplete');
    expect(metaFigureOf(chili, true)).toBe('calories incomplete');
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

describe('estimates', () => {
  const recipe = {
    groups: [
      {
        ingredients: ['butter', 'onion', 'salt'].map((name) => ({ id: name, name }))
      }
    ]
  } as never;

  const line = (ingredientId: string, status: string, via: string | null) =>
    ({ ingredientId, status, via, canRaiseEnergy: false }) as never;

  it('marks the meta figure with a word, and says it for a screen reader after the figure', () => {
    const rough = answer({
      values: { energyKcal: { value: 520.4, atLeast: false, estimated: true } } as never
    });

    expect(plain(metaFigureOf(rough))).toBe('about 520 kcal');
    expect(plain(metaFigureOf(rough, true))).toBe('520 kcal, partly estimated');
    expect(
      plain(
        metaFigureOf(
          answer({
            values: { energyKcal: { value: 520.9, atLeast: true, estimated: true } } as never
          }),
          true
        )
      )
    ).toBe('at least 520 kcal, partly estimated');
  });

  it('names the lines counted by a typical weight, in the recipe order, and not the others', () => {
    const rough = answer({
      ingredients: [
        line('butter', 'counted', 'householdWeight'),
        line('onion', 'counted', 'typicalWeight'),
        line('salt', 'counted', 'typicalWeight')
      ] as never
    });

    expect(estimatedNames(rough, recipe)).toEqual(['onion', 'salt']);
  });

  it('words them by the same rule as what is missing', () => {
    expect(estimatedWords([])).toBeNull();
    expect(estimatedWords(['onion'])).toBe('partly estimated: onion');
    expect(estimatedWords(['onion', 'salt'])).toBe('partly estimated: onion and salt');
    expect(estimatedWords(['a', 'b', 'c', 'd'])).toBe('partly estimated: a, b and 2 more');

    preferences.setLocale('de');

    expect(estimatedWords(['Zwiebel', 'Salz', 'Öl'])).toBe(
      'teils geschätzt: Zwiebel, Salz und 1 weitere'
    );
  });
});
