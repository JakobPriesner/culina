import { screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { recipeAnswers } from '$features/nutrition/stores/recipeAnswers.svelte';
import { renderWithProviders } from '$lib/test/render';
import { preferences } from '$shell/preferences.svelte';

import type { PlannedMeal } from './mealPlan.svelte';
import PlanDayNutrition from './PlanDayNutrition.svelte';

const answer = (kcal: number, over: object = {}) => ({
  per: 'serving',
  yield: 4,
  complete: true,
  counted: 3,
  lines: 3,
  values: { energyKcal: { value: kcal, atLeast: false } },
  ingredients: [],
  source: { name: 'BLS', version: '4.0', publisher: 'MRI', licence: 'CC BY 4.0' },
  ...over
});

const meal = (recipeId: string, title: string) => ({ entryId: `e-${recipeId}`, recipeId, title });

const answers: Record<string, object> = {
  soup: answer(450),
  pasta: answer(700),
  cake: answer(300, { per: 'piece', yield: 12 })
};

/** Answers by recipe id, and counts what was asked. */
function server() {
  const asked: string[] = [];

  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request) => {
      const id = new URL(input.url).pathname.split('/')[4]!;
      const body = answers[id];

      asked.push(id);

      return Promise.resolve(
        body
          ? new Response(JSON.stringify(body), {
              headers: { 'Content-Type': 'application/json' }
            })
          : new Response(null, { status: 503 })
      );
    })
  );

  return asked;
}

const show = (...meals: ReturnType<typeof meal>[]) =>
  renderWithProviders(PlanDayNutrition, {
    props: { meals: meals as unknown as PlannedMeal[], householdId: 'h1' }
  });

beforeEach(() => preferences.setLocale('en'));

afterEach(() => {
  recipeAnswers.reset();
  vi.unstubAllGlobals();
});

describe('a planned day, per person', () => {
  it('adds each planned recipe once its answer is in, and shows nothing before', async () => {
    server();

    const meals = [meal('soup', 'Soup'), meal('pasta', 'Pasta')];
    const { container } = show(...meals);

    expect(container.querySelector('[data-plan-nutrition]')).toBeNull();

    await recipeAnswers.ensure(['soup', 'pasta'], 'h1');

    expect(await screen.findByText(/1,150\s+kcal per person/)).toBeInTheDocument();
  });

  it('waits for every meal: a sum that grows as answers arrive would be too small for a while', async () => {
    server();

    const { container } = show(meal('soup', 'Soup'), meal('pasta', 'Pasta'));

    await recipeAnswers.ensure(['soup'], 'h1');

    expect(container.querySelector('[data-plan-nutrition]')).toBeNull();
  });

  it('says what a piece recipe or a failed read left out', async () => {
    server();
    show(meal('soup', 'Soup'), meal('cake', 'Cake'), meal('gone', 'Gone'));

    await recipeAnswers.ensure(['soup', 'cake', 'gone'], 'h1');

    expect(
      await screen.findByText(/at least 450\s+kcal per person · without Cake and Gone/)
    ).toBeInTheDocument();
  });

  it('shows nothing for a day with no meals or nothing to add up', async () => {
    server();

    const { container } = show(meal('cake', 'Cake'));

    await recipeAnswers.ensure(['cake'], 'h1');
    await waitFor(() => expect(recipeAnswers.of('cake', 'h1').settled).toBe(true));

    expect(container.querySelector('[data-plan-nutrition]')).toBeNull();
  });
});

describe('asking for the planned recipes', () => {
  it('asks once per recipe and household, all at once', async () => {
    const asked = server();

    await Promise.all([
      recipeAnswers.ensure(['soup', 'pasta'], 'h1'),
      recipeAnswers.ensure(['soup'], 'h1')
    ]);
    await recipeAnswers.ensure(['soup', 'pasta'], 'h1');
    await recipeAnswers.ensure(['soup'], 'h2');

    expect(asked.toSorted()).toEqual(['pasta', 'soup', 'soup']);
  });

  it('asks again after a reset, and drops an answer that was still on its way', async () => {
    const asked = server();

    const first = recipeAnswers.ensure(['soup'], 'h1');

    recipeAnswers.reset();
    await first;

    expect(recipeAnswers.of('soup', 'h1').settled).toBe(false);

    await recipeAnswers.ensure(['soup'], 'h1');

    expect(asked).toEqual(['soup', 'soup']);
    expect(recipeAnswers.of('soup', 'h1').settled).toBe(true);
  });
});
