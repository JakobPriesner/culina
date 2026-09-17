import { describe, expect, it } from 'vitest';

import { gapToRestore, placeOf, withMealMoved, type PlannedDay } from './mealPlan.svelte';

/**
 * Where a dropped card actually lands.
 *
 * The optimistic half of a move: this runs before the server answers, and it
 * has to agree with what the server will say — a card that lands in one place
 * under the finger and jumps to another when the response arrives is worse than
 * one that never moved.
 */
const monday = '2026-09-14';
const tuesday = '2026-09-15';

const meal = (title: string, slot = 'dinner') => ({
  entryId: `entry-${title}`,
  recipeId: `recipe-${title}`,
  title,
  recipeServings: 4,
  slot
});

const week = (days: Record<string, ReturnType<typeof meal>[]>) => ({
  from: monday,
  days: Object.entries(days).map(([date, meals]) => ({ date, meals })) as PlannedDay[]
});

/** The titles of a day, which is the only thing these assertions are about. */
const titles = (result: ReturnType<typeof week>, date: string) =>
  result.days.find((day) => day.date === date)?.meals.map((one) => one.title);

describe('moving a meal to another day', () => {
  it('takes it off the day it came from', () => {
    const start = week({ [monday]: [meal('Curry')], [tuesday]: [] });

    const moved = withMealMoved(start, 'entry-Curry', { date: tuesday });

    expect(titles(moved, monday)).toEqual([]);
    expect(titles(moved, tuesday)).toEqual(['Curry']);
  });

  it('keeps the servings and the slot it was planned with', () => {
    const planned = { ...meal('Curry', 'lunch'), servings: 6 };
    const start = week({ [monday]: [planned], [tuesday]: [] });

    const moved = withMealMoved(start, 'entry-Curry', { date: tuesday });
    const landed = moved.days.find((day) => day.date === tuesday)?.meals[0];

    // Moving a meal is not re-planning it. Doing this as a remove plus an add
    // is exactly how both of these are lost.
    expect(landed?.servings).toBe(6);
    expect(landed?.slot).toBe('lunch');
  });

  it('puts it last when no gap was aimed at, which is what the sheet sends', () => {
    const start = week({ [monday]: [meal('Curry')], [tuesday]: [meal('Soup')] });

    const moved = withMealMoved(start, 'entry-Curry', { date: tuesday });

    expect(titles(moved, tuesday)).toEqual(['Soup', 'Curry']);
  });

  it('drops it into the gap it was aimed at', () => {
    const start = week({
      [monday]: [meal('Curry')],
      [tuesday]: [meal('Soup'), meal('Stew')]
    });

    const moved = withMealMoved(start, 'entry-Curry', { date: tuesday, position: 1 });

    expect(titles(moved, tuesday)).toEqual(['Soup', 'Curry', 'Stew']);
  });

  it('settles a breakfast among the breakfasts wherever it was let go', () => {
    // A day is read in slot order first, so this is where the server will put
    // it. Landing it under the finger and then watching it jump when the
    // response arrives is the thing this agreement prevents.
    const start = week({
      [monday]: [meal('Toast', 'breakfast')],
      [tuesday]: [meal('Porridge', 'breakfast'), meal('Stew')]
    });

    const moved = withMealMoved(start, 'entry-Toast', { date: tuesday, position: 2 });

    expect(titles(moved, tuesday)).toEqual(['Porridge', 'Toast', 'Stew']);
  });
});

describe('reordering within one day', () => {
  // The gaps are counted with the meal being moved still in place, which is how
  // the day is drawn while a card is in the air.
  it.each([
    [0, ['Third', 'First', 'Second']],
    [1, ['First', 'Third', 'Second']],
    [3, ['First', 'Second', 'Third']]
  ])('drops the last of three into gap %i', (position, expected) => {
    const start = week({ [monday]: [meal('First'), meal('Second'), meal('Third')] });

    expect(titles(withMealMoved(start, 'entry-Third', { date: monday, position }), monday)).toEqual(
      expected
    );
  });

  it('means the same thing going down as going up', () => {
    // Moving down is where a reorder goes one off: the meal vacates a place
    // above its destination on the way past.
    const start = week({ [monday]: [meal('First'), meal('Second'), meal('Third')] });

    const moved = withMealMoved(start, 'entry-First', { date: monday, position: 3 });

    expect(titles(moved, monday)).toEqual(['Second', 'Third', 'First']);
  });
});

describe('putting it back', () => {
  it('restores a meal that crossed to another day', () => {
    const before = { date: monday, slot: 'dinner' as const, index: 1 };
    const after = { date: tuesday, slot: 'dinner' as const, index: 0 };

    expect(gapToRestore(before, after)).toBe(1);
  });

  it('asks for the gap below its old place when it was dragged up the day', () => {
    const before = { date: monday, slot: 'dinner' as const, index: 2 };
    const after = { date: monday, slot: 'dinner' as const, index: 0 };

    // It is sitting above its old place now, so the gap numbered 2 is one above
    // where it belongs. The meal in the air does not count as a place.
    expect(gapToRestore(before, after)).toBe(3);
  });

  it('asks for its old place unchanged when it was dragged down the day', () => {
    const before = { date: monday, slot: 'dinner' as const, index: 0 };
    const after = { date: monday, slot: 'dinner' as const, index: 2 };

    // Nothing above it moved, so the gap it left is still numbered the same.
    expect(gapToRestore(before, after)).toBe(0);
  });

  it('undoes a move, all the way round', () => {
    const start = week({ [monday]: [meal('First'), meal('Second'), meal('Third')] });

    const before = placeOf(start.days, 'entry-First')!;
    const moved = withMealMoved(start, 'entry-First', { date: monday, position: 3 });
    const after = placeOf(moved.days, 'entry-First')!;

    const back = withMealMoved(moved, 'entry-First', {
      date: before.date,
      position: gapToRestore(before, after)
    });

    expect(titles(back, monday)).toEqual(['First', 'Second', 'Third']);
  });
});

describe('finding a meal', () => {
  it('says which day it is on and where in it', () => {
    const start = week({ [monday]: [meal('Curry')], [tuesday]: [meal('Soup'), meal('Stew')] });

    expect(placeOf(start.days, 'entry-Stew')).toEqual({
      date: tuesday,
      slot: 'dinner',
      index: 1
    });
  });

  it('is null for a meal the week does not have', () => {
    expect(placeOf(week({ [monday]: [] }).days, 'entry-Nothing')).toBeNull();
  });
});
