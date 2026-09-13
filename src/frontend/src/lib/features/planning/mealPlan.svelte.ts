import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

import type { components } from '$api/generated/schema';

/**
 * What this household means to cook this week.
 *
 * A week, not a calendar. A week is the unit people actually plan in, and a
 * month view arrives with recurrence, drag-and-drop and a second reason for a
 * shopping list to exist.
 *
 * Every write returns the whole week, because the week is the screen: a
 * response carrying one changed entry would leave this refetching the rest to
 * draw anything.
 */
type Week = components['schemas']['PlanningMealPlanResponse'];

export type PlannedDay = Week['days'][number];
export type PlannedMeal = PlannedDay['meals'][number];

/** Which meal of the day. Omitted means dinner, which is what people plan. */
export type MealSlot = 'breakfast' | 'lunch' | 'dinner';

/** The date as the API spells it, which is also how a day is identified. */
export const asDate = (day: Date): string =>
  `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`;

class MealPlanStore {
  #week = $state<Week | null>(null);
  #error = $state<AppError | null>(null);
  #loading = $state(false);

  get days(): readonly PlannedDay[] {
    return this.#week?.days ?? [];
  }

  /** The Monday the week on screen starts on, as the API spells it. */
  get from(): string | null {
    return this.#week?.from ?? null;
  }

  get error(): AppError | null {
    return this.#error;
  }

  get loading(): boolean {
    return this.#loading;
  }

  /** Every planned meal of the week, flattened, in the order it will be cooked. */
  get meals(): readonly PlannedMeal[] {
    return this.days.flatMap((day) => day.meals);
  }

  async load(householdId: string, from?: string): Promise<void> {
    // The week already on screen stays while the next one arrives. Replacing it
    // with a skeleton to show the same seven days again loses your place.
    this.#loading = true;

    const result = await request(() =>
      http.GET('/api/v1/households/{householdId}/meal-plan', {
        params: { path: { householdId }, query: from ? { from } : {} }
      })
    );

    this.#loading = false;

    if (result.ok) {
      this.#week = result.value;
      this.#error = null;

      return;
    }

    this.#error = result.error;
  }

  async plan(
    householdId: string,
    meal: { date: string; recipeId: string; servings?: number; slot?: MealSlot }
  ): Promise<boolean> {
    const result = await request(() =>
      http.POST('/api/v1/households/{householdId}/meal-plan', {
        params: { path: { householdId } },
        body: meal
      })
    );

    if (result.ok) {
      this.#week = result.value;
      this.#error = null;
    } else {
      this.#error = result.error;
    }

    return result.ok;
  }

  async unplan(householdId: string, entryId: string): Promise<boolean> {
    const result = await request(() =>
      http.DELETE('/api/v1/households/{householdId}/meal-plan/{entryId}', {
        params: { path: { householdId, entryId } }
      })
    );

    if (result.ok) {
      this.#week = result.value;
      this.#error = null;
    } else {
      this.#error = result.error;
    }

    return result.ok;
  }

  reset(): void {
    this.#week = null;
    this.#error = null;
    this.#loading = false;
  }
}

export const mealPlan = new MealPlanStore();

registerStore(() => mealPlan.reset());
