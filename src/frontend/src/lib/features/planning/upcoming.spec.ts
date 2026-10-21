import { afterEach, describe, expect, it, vi } from 'vitest';

import { mealPlan, type PlannedMeal } from './mealPlan.svelte';
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
  mealPlan.reset();
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

describe('tonight, beside the week being edited', () => {
  it('follows a change made on the plan page at once, without asking the server again', async () => {
    vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-10-17T18:00:00') });
    vi.stubGlobal('fetch', vi.fn(weekWith([meal('dinner', 'dinner')])));

    await upcoming.load('h1');
    await mealPlan.load('h1', '2026-10-12');

    const fetched = vi.fn(() =>
      Promise.resolve(
        new Response(
          JSON.stringify({
            from: '2026-10-12',
            days: [{ date: '2026-10-17', meals: [{ ...meal('dinner', 'dinner'), servings: 4 }] }]
          }),
          { headers: { 'Content-Type': 'application/json' } }
        )
      )
    );
    vi.stubGlobal('fetch', fetched);

    // Optimistic: the week shows the new servings before the answer, and so does tonight's meal.
    const moving = mealPlan.move('h1', 'dinner', { date: '2026-10-17', servings: 4 });

    expect(upcoming.mealNow?.servings).toBe(4);

    await moving;

    expect(upcoming.mealNow?.servings).toBe(4);
  });

  it('drops a meal deleted on the week', async () => {
    vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-10-17T18:00:00') });
    vi.stubGlobal('fetch', vi.fn(weekWith([meal('dinner', 'dinner')])));

    await upcoming.load('h1');
    await mealPlan.load('h1', '2026-10-12');

    // The server answers every write with the whole week, today now empty.
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve(
          new Response(
            JSON.stringify({ from: '2026-10-12', days: [{ date: '2026-10-17', meals: [] }] }),
            { headers: { 'Content-Type': 'application/json' } }
          )
        )
      )
    );
    await mealPlan.unplan('h1', 'dinner');

    expect(upcoming.mealNow).toBeNull();
  });
});

describe('a meal of several dishes', () => {
  it('names every dish of the meal now, and finds a dish’s companions', async () => {
    vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-10-17T18:00:00') });
    vi.stubGlobal(
      'fetch',
      vi.fn(weekWith([meal('curry', 'dinner'), meal('rice', 'dinner'), meal('toast', 'breakfast')]))
    );

    await upcoming.load('h1');

    expect(upcoming.dishesNow.map((one) => one.entryId)).toEqual(['curry', 'rice']);
    expect(upcoming.dishesWith('r-rice').map((one) => one.entryId)).toEqual(['curry', 'rice']);
    expect(upcoming.dishesWith('r-nothing')).toEqual([]);
  });
});
