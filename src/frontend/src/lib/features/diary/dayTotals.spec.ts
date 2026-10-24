import { describe, expect, it } from 'vitest';

import { dayWith, requestOf, sameThing, unlogged } from './dayTotals';
import type { DiaryEntry } from './types';

const entry = (over: Partial<DiaryEntry>): DiaryEntry => ({
  entryId: crypto.randomUUID(),
  date: '2026-10-22',
  meal: 'lunch',
  source: 'food',
  label: 'Haferflocken',
  amount: 50,
  unit: 'g',
  grams: 50,
  figures: { kcal: 186, protein: 6.75, carbohydrate: 29.5, fat: 3.5 },
  atLeast: false,
  estimated: false,
  foodCode: 'C133000',
  version: 1,
  ...over
});

describe('a day added up', () => {
  it('sums what is known, as the server does', () => {
    const day = dayWith('2026-10-22', [
      entry({}),
      entry({ figures: { kcal: 400, protein: null } })
    ]);

    expect(day.totals.kcal).toBe(586);
    expect(day.totals.protein).toBe(6.75);
    expect(day.atLeast).toBe(false);
  });

  it('is a lower bound when an entry is one, or has no energy at all', () => {
    expect(dayWith('2026-10-22', [entry({ atLeast: true })]).atLeast).toBe(true);
    expect(dayWith('2026-10-22', [entry({ figures: { kcal: null } })]).atLeast).toBe(true);
  });

  it('says how much of it the person typed, and orders meals through the day', () => {
    const day = dayWith('2026-10-22', [
      entry({ meal: 'snack', source: 'typed', label: 'Kuchen', figures: { kcal: 400 } }),
      entry({ meal: 'breakfast' })
    ]);

    expect(day.typedKcal).toBe(400);
    expect(day.entries.map((one) => one.meal)).toEqual(['breakfast', 'snack']);
  });

  it('knows nothing when nothing was eaten, rather than zero', () => {
    const day = dayWith('2026-10-22', []);

    expect(day.totals.kcal).toBeNull();
    expect(day.entries).toEqual([]);
  });
});

describe('logging the same thing again', () => {
  it('asks the server to look a recipe or a food up afresh, never sending figures', () => {
    const recipe = requestOf(
      entry({ source: 'recipe', recipeId: 'r1', householdId: 'h1', amount: 1.5, unit: 'portion' }),
      '2026-10-23',
      'dinner'
    );
    const food = requestOf(entry({ unit: 'piece', amount: 2 }), '2026-10-23', 'snack');

    expect(recipe).toEqual({
      date: '2026-10-23',
      meal: 'dinner',
      recipeId: 'r1',
      portions: 1.5,
      householdId: 'h1'
    });
    expect(food).toMatchObject({ foodCode: 'C133000', amount: 2, unit: 'piece' });
    expect(food).not.toHaveProperty('kcal');
  });

  it('carries a typed entry over as typed, unknown macros unknown', () => {
    const typed = requestOf(
      entry({ source: 'typed', label: 'Café-Lunch', figures: { kcal: 650 } }),
      '2026-10-23',
      'lunch'
    );

    expect(typed).toMatchObject({ label: 'Café-Lunch', kcal: 650, protein: null });
  });

  it('treats the same recipe, food or typed name as the same thing eaten', () => {
    expect(sameThing(entry({ recipeId: 'r1', foodCode: null }))).toBe('recipe:r1');
    expect(sameThing(entry({}))).toBe('food:C133000');
    expect(sameThing(entry({ source: 'typed', foodCode: null, label: ' Kuchen ' }))).toBe(
      'typed:kuchen'
    );
  });
});

describe('the plan as suggestions', () => {
  const planned = (slot: string, recipeId = 'lasagne') => ({ slot, recipeId });

  it('keeps leftovers for dinner when the dish was logged at lunch', () => {
    const left = unlogged(
      [planned('lunch'), planned('dinner')],
      [entry({ source: 'recipe', recipeId: 'lasagne', meal: 'lunch' })]
    );

    expect(left).toEqual([planned('dinner')]);
  });

  it('counts a portion logged at another meal towards the plan', () => {
    const left = unlogged(
      [planned('dinner')],
      [entry({ source: 'recipe', recipeId: 'lasagne', meal: 'lunch' })]
    );

    expect(left).toEqual([]);
  });

  it('offers what nothing was logged for', () => {
    const left = unlogged(
      [planned('lunch', 'soup')],
      [entry({ source: 'recipe', recipeId: 'lasagne', meal: 'lunch' })]
    );

    expect(left).toEqual([planned('lunch', 'soup')]);
  });
});
