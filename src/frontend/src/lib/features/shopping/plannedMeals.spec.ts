import { describe, expect, it } from 'vitest';

import { plannedMealsOf } from './plannedMeals';
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
    const days = plannedMealsOf(
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
    const days = plannedMealsOf(
      [line('Mehl', [source('Waffeln', '2026-09-23', 'dinner')])],
      '2026-10-14'
    );

    expect(days).toHaveLength(1);
    expect(days[0]!.isPast).toBe(true);
    expect(days[0]!.meals[0]!.lines).toHaveLength(1);
  });

  it('counts today and later as current', () => {
    const days = plannedMealsOf(
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

  it('leaves out lines already bought and sources added without a day', () => {
    const days = plannedMealsOf(
      [
        line('Mehl', [source('Waffeln', '2026-09-23', 'dinner')], true),
        line('Salz', [source('Brot', null, null)]),
        line('Zucker', [])
      ],
      '2026-10-14'
    );

    expect(days).toEqual([]);
  });
});
