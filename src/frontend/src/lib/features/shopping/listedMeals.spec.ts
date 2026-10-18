import { describe, expect, it } from 'vitest';

import { listedMealsOf } from './listedMeals';
import type { ShoppingItem } from './stores/shopping.svelte';

const line = (name: string, sources: ShoppingItem['sources'], isChecked = false): ShoppingItem => ({
  itemId: name,
  name,
  quantity: 1,
  unit: null,
  section: 'produce',
  isChecked,
  isManual: false,
  sources
});

const source = (recipeTitle: string, plannedDate: string | null, plannedSlot: string | null) => ({
  recipeId: recipeTitle.toLowerCase(),
  recipeTitle,
  quantity: 1,
  unit: null,
  plannedDate,
  plannedSlot
});

describe('the planned meals behind the shopping list', () => {
  it('names each meal once, with every line it asks for, in day and meal order', () => {
    const days = listedMealsOf(
      [
        line('Mehl', [
          source('Waffeln', '2026-10-14', 'breakfast'),
          source('Pizza', '2026-10-14', 'dinner')
        ]),
        line('Eier', [source('Waffeln', '2026-10-14', 'breakfast')]),
        line('Hefe', [source('Pizza', '2026-10-13', 'dinner')])
      ],
      '2026-10-14'
    );

    expect(days.map((day) => day.date)).toEqual(['2026-10-13', '2026-10-14']);
    expect(days[1]!.meals.map((meal) => meal.recipeTitle)).toEqual(['Waffeln', 'Pizza']);
    expect(days[1]!.meals[0]!.lines.map((l) => l.name)).toEqual(['Mehl', 'Eier']);
  });

  it('marks the days before today as past, and keeps their lines', () => {
    const days = listedMealsOf(
      [line('Mehl', [source('Waffeln', '2026-09-23', 'dinner')])],
      '2026-10-14'
    );

    expect(days).toHaveLength(1);
    expect(days[0]!.isPast).toBe(true);
    expect(days[0]!.meals[0]!.lines).toHaveLength(1);
  });

  it('counts today and later as current', () => {
    const days = listedMealsOf(
      [
        line('Mehl', [
          source('Waffeln', '2026-10-14', 'dinner'),
          source('Pizza', '2026-10-20', null)
        ])
      ],
      '2026-10-14'
    );

    expect(days.map((day) => day.isPast)).toEqual([false, false]);
  });

  it('lists a recipe added without a day last, so it can be taken back off as well', () => {
    const days = listedMealsOf(
      [
        line('Salz', [source('Brot', null, null)]),
        line('Mehl', [source('Waffeln', '2026-10-14', 'dinner')])
      ],
      '2026-10-14'
    );

    expect(days.map((day) => day.date)).toEqual(['2026-10-14', null]);
    expect(days[1]!.meals[0]!.recipeTitle).toBe('Brot');
    expect(days[1]!.isPast).toBe(false);
  });

  it('counts bought lines as done, and drops a meal once all of it is bought', () => {
    const days = listedMealsOf(
      [
        line('Mehl', [source('Waffeln', '2026-10-14', 'dinner')], true),
        line('Eier', [source('Waffeln', '2026-10-14', 'dinner')]),
        line('Hefe', [source('Pizza', '2026-10-14', 'dinner')], true),
        line('Zucker', [])
      ],
      '2026-10-14'
    );

    expect(days[0]!.meals.map((meal) => [meal.recipeTitle, meal.done, meal.lines.length])).toEqual([
      ['Waffeln', 1, 2]
    ]);
  });

  it('names a line once for a recipe added by itself twice, with both amounts', () => {
    const days = listedMealsOf(
      [line('Salz', [source('Brot', null, null), source('Brot', null, null)])],
      '2026-10-14'
    );

    expect(days[0]!.meals[0]!.lines).toEqual([
      expect.objectContaining({ name: 'Salz', quantity: 2 })
    ]);
  });
});
