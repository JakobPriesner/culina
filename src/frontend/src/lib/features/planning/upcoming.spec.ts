import { afterEach, describe, expect, it, vi } from 'vitest';

import type { PlannedMeal } from './mealPlan.svelte';
import { upcoming } from './upcoming.svelte';

/* Today's row on the week leads to the meal still to cook, and says the day is done once it is. */
const meal = (entryId: string, slot: string, cookedAt: string | null = null): PlannedMeal => ({
  entryId,
  recipeId: `r-${entryId}`,
  title: entryId,
  servings: 6,
  recipeServings: 4,
  slot,
  isOnShoppingList: false,
  cookedAt
});

const weekWith = (meals: PlannedMeal[]) => (input: Request) => {
  const from = new URL(input.url).searchParams.get('from');
  const days = from === '2026-10-12' ? [{ date: '2026-10-17', meals }] : [];

  return Promise.resolve(
    new Response(JSON.stringify({ from, days }), {
      headers: { 'Content-Type': 'application/json' }
    })
  );
};

afterEach(() => {
  upcoming.reset();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("today's meal", () => {
  it('is the one of this part of the day, and the next once that one is cooked', async () => {
    vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-10-17T12:30:00') });
    vi.stubGlobal('fetch', vi.fn(weekWith([meal('lunch', 'lunch'), meal('dinner', 'dinner')])));

    await upcoming.load('h1');

    expect(upcoming.mealNow?.entryId).toBe('lunch');
    expect(upcoming.nextFor('r-lunch')?.servings).toBe(6);
    expect(upcoming.mealOf('dinner')?.title).toBe('dinner');

    vi.stubGlobal(
      'fetch',
      vi.fn(weekWith([meal('lunch', 'lunch', '2026-10-17T13:10:00'), meal('dinner', 'dinner')]))
    );
    await upcoming.load('h1');

    expect(upcoming.mealNow?.entryId).toBe('dinner');
    expect(upcoming.cookedToday.map((one) => one.entryId)).toEqual(['lunch']);
  });

  it('is none once everything planned today is cooked, and a cook on another day does not count', async () => {
    vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-10-17T19:00:00') });
    vi.stubGlobal(
      'fetch',
      vi.fn(
        weekWith([
          meal('lunch', 'lunch', '2026-10-16T12:00:00'),
          meal('dinner', 'dinner', '2026-10-17T19:45:00')
        ])
      )
    );

    await upcoming.load('h1');

    expect(upcoming.cookedToday.map((one) => one.entryId)).toEqual(['dinner']);
    expect(upcoming.mealNow?.entryId).toBe('lunch');
  });
});
