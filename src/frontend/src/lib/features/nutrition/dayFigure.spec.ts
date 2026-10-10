import { beforeEach, describe, expect, it } from 'vitest';

import { dayFigure, dayWords, type PlannedFigure } from './dayFigure';
import type { Nutrition } from './types';
import { preferences } from '$shell/preferences.svelte';

const answer = (kcal: number, over: Partial<Nutrition> & { atLeast?: boolean } = {}): Nutrition => {
  const { atLeast = false, ...rest } = over;

  return {
    per: 'serving',
    yield: 4,
    complete: !atLeast,
    counted: 3,
    lines: 3,
    values: { energyKcal: { value: kcal, atLeast } },
    ingredients: [],
    ...rest
  } as unknown as Nutrition;
};

const meal = (title: string, nutrition: Nutrition | null): PlannedFigure => ({ title, nutrition });
const plain = (text: string | null) => text?.replace(/\u00a0/g, ' ') ?? null;

beforeEach(() => preferences.setLocale('en'));

describe('what one person has on a planned day', () => {
  it('adds one portion of each meal, exactly when every part is exact', () => {
    const words = dayWords([meal('Soup', answer(450)), meal('Pasta', answer(700.4))]);

    expect(plain(words)).toBe('1,150 kcal per person');
  });

  it('is a lower bound when a part is, and rounds down', () => {
    const words = dayWords([
      meal('Soup', answer(450.6, { atLeast: true })),
      meal('Pasta', answer(700))
    ]);

    expect(plain(words)).toBe('at least 1,150 kcal per person');
  });

  it('leaves out a recipe of pieces, and says so', () => {
    const words = dayWords([
      meal('Soup', answer(450)),
      meal('Cake', answer(300, { per: 'piece', yield: 12 }))
    ]);

    expect(plain(words)).toBe('at least 450 kcal per person · without Cake');
  });

  it('leaves out a recipe that makes one serving: that is the whole pot', () => {
    const words = dayWords([meal('Soup', answer(450)), meal('Stew', answer(2400, { yield: 1 }))]);

    expect(plain(words)).toBe('at least 450 kcal per person · without Stew');
  });

  it('leaves out what could not be counted or read, by name, once each', () => {
    const words = dayWords([
      meal('Soup', answer(450)),
      meal('Mystery', answer(0, { counted: 0, atLeast: true })),
      meal('Unread', null),
      meal('Mystery', answer(0, { counted: 0, atLeast: true }))
    ]);

    expect(plain(words)).toBe('at least 450 kcal per person · without Mystery and Unread');
  });

  it('names two and counts the rest', () => {
    const words = dayWords([
      meal('Soup', answer(450)),
      meal('A', null),
      meal('B', null),
      meal('C', null),
      meal('D', null)
    ]);

    expect(plain(words)).toBe('at least 450 kcal per person · without A, B and 2 more');
  });

  it('says nothing when nothing could be added, or there is no meal', () => {
    expect(dayFigure([])).toBeNull();
    expect(
      dayWords([meal('Cake', answer(300, { per: 'piece' })), meal('Unread', null)])
    ).toBeNull();
  });

  it('speaks German', () => {
    preferences.setLocale('de');

    expect(plain(dayWords([meal('Suppe', answer(1850.4, { atLeast: true }))]))).toBe(
      'mind. 1.850 kcal pro Person'
    );
  });
});
