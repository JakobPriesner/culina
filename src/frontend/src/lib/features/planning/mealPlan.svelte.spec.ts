import { afterEach, describe, expect, it, vi } from 'vitest';

import { mealPlan, weeksOf } from './mealPlan.svelte';

/** The days on screen are any run of days; the server answers in Monday weeks (docs/woche-design.md §10). */
const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

/** A week with one meal on its Monday, as the server answers for any day of it. */
const weekFrom = (monday: string) => ({
  from: monday,
  days: Array.from({ length: 7 }, (_, index) => {
    const day = new Date(`${monday}T12:00:00`);
    day.setDate(day.getDate() + index);
    const date = `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`;

    return {
      date,
      meals:
        index === 0
          ? [
              {
                entryId: `e-${date}`,
                recipeId: 'r',
                title: date,
                recipeServings: 2,
                slot: 'dinner'
              }
            ]
          : []
    };
  })
});

describe('days across two weeks', () => {
  afterEach(() => {
    mealPlan.reset();
    vi.unstubAllGlobals();
  });

  it('are the weeks the run of days touches', () => {
    expect(weeksOf('2026-09-19', 4)).toEqual(['2026-09-14', '2026-09-21']);
    expect(weeksOf('2026-09-14', 7)).toEqual(['2026-09-14']);
  });

  it('show Saturday to Tuesday from the two weeks they fall in', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) => json(weekFrom(new URL(input.url).searchParams.get('from')!)))
    );

    await mealPlan.load('h1', '2026-09-19', 4);

    expect(mealPlan.days.map((day) => day.date)).toEqual([
      '2026-09-19',
      '2026-09-20',
      '2026-09-21',
      '2026-09-22'
    ]);
    expect(mealPlan.meals.map((meal) => meal.title)).toEqual(['2026-09-21']);
    expect(mealPlan.weeks).toEqual(['2026-09-14', '2026-09-21']);
  });

  it('are read once each when an effect shows them, never again on their own answers', async () => {
    const fetch = vi.fn(async (input: Request) =>
      json(weekFrom(new URL(input.url).searchParams.get('from')!))
    );
    vi.stubGlobal('fetch', fetch);

    const stop = $effect.root(() => {
      $effect(() => {
        void mealPlan.load('h1', '2026-09-19', 4);
      });
    });

    await vi.waitFor(() => expect(mealPlan.days).toHaveLength(4));
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(fetch).toHaveBeenCalledTimes(2);
    stop();
  });

  it('move a meal over Sunday night and read the week it left again', async () => {
    const asked: string[] = [];
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) => {
        if (input.method === 'GET') {
          const from = new URL(input.url).searchParams.get('from')!;
          asked.push(from);

          return json(weekFrom(from));
        }

        // The meal lands on next Monday; the answer is that week.
        const next = weekFrom('2026-09-21');
        next.days[0]!.meals.push({
          entryId: 'e-2026-09-14',
          recipeId: 'r',
          title: '2026-09-14',
          recipeServings: 2,
          slot: 'dinner'
        });

        return json(next);
      })
    );

    await mealPlan.load('h1', '2026-09-14', 14);
    asked.length = 0;

    expect(await mealPlan.move('h1', 'e-2026-09-14', { date: '2026-09-21' })).toBe(true);

    expect(
      mealPlan.days.find((day) => day.date === '2026-09-21')?.meals.map((meal) => meal.entryId)
    ).toEqual(['e-2026-09-21', 'e-2026-09-14']);
    await vi.waitFor(() => expect(asked).toEqual(['2026-09-14']));
  });
});
