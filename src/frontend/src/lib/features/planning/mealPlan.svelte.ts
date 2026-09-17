import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

import type { components } from '$api/generated/schema';

/**
 * What this household means to cook this week.
 *
 * A week, not a calendar. A week is the unit people actually plan in, and a
 * month view arrives with recurrence and a second reason for a shopping list to
 * exist.
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

/** Where a meal is being put. */
export interface MealDestination {
  readonly date: string;
  /** Omitted keeps the slot it had, which is what dragging does. */
  readonly slot?: MealSlot;
  /**
   * Which gap of the target day it was dropped into, counted with the meal
   * being moved still in place. Omitted puts it last.
   */
  readonly position?: number;
}

/** Where a meal is, so it can be put back there. */
export interface MealPlace {
  readonly date: string;
  readonly slot: MealSlot;
  readonly index: number;
}

/** Slot order, which the server sorts a day by before anything else. */
const slotRank = { breakfast: 0, lunch: 1, dinner: 2 } as const;

/** Where a slot sits in a day. An unknown one sorts with dinner, as it reads. */
const rankOf = (slot: string): number => slotRank[slot as keyof typeof slotRank] ?? slotRank.dinner;

/** Where a meal is in the week, or null when it is not in it. */
export const placeOf = (week: readonly PlannedDay[], entryId: string): MealPlace | null => {
  for (const day of week) {
    const index = day.meals.findIndex((meal) => meal.entryId === entryId);
    const found = day.meals[index];

    if (found) {
      return { date: day.date, slot: found.slot as MealSlot, index };
    }
  }

  return null;
};

/**
 * The gap that puts a meal back where it was.
 *
 * A position counts the gaps of the day with the meal still in it, so undoing
 * a move down the same day needs one more than the index it started at — the
 * meal is no longer occupying the place above its old one. Across days there
 * is nothing to account for.
 */
export const gapToRestore = (before: MealPlace, after: MealPlace): number =>
  before.date === after.date && after.index < before.index ? before.index + 1 : before.index;

/**
 * The week with one meal somewhere else.
 *
 * Pure, and the same arrangement the server will send back: a day is read in
 * slot order first, so a breakfast dropped below a dinner settles at the end of
 * the breakfasts here too rather than jumping there when the response lands.
 */
export const withMealMoved = (week: Week, entryId: string, to: MealDestination): Week => {
  const from = placeOf(week.days, entryId);
  const meal = from && week.days.find((day) => day.date === from.date)?.meals[from.index];

  if (!from || !meal) {
    return week;
  }

  const moved = { ...meal, slot: to.slot ?? meal.slot };

  // Counted against the day as it is on screen, which still has the meal in it
  // when the day is the one it came from.
  const gap = to.position ?? Number.MAX_SAFE_INTEGER;
  const landing = to.date === from.date && from.index < gap ? gap - 1 : gap;

  return {
    ...week,
    days: week.days.map((day) => {
      if (day.date !== from.date && day.date !== to.date) {
        return day;
      }

      const without = day.meals.filter((one) => one.entryId !== entryId);

      if (day.date !== to.date) {
        return { ...day, meals: without };
      }

      const meals = [...without.slice(0, landing), moved, ...without.slice(landing)];

      return {
        ...day,
        meals: [...meals].sort((one, other) => rankOf(one.slot) - rankOf(other.slot))
      };
    })
  };
};

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

  /**
   * The same meal, on another day.
   *
   * Optimistic, because this is a drag: the card has to arrive under the finger
   * that dropped it, and waiting for a round trip to draw it there is what
   * makes a drag feel broken. The whole week is the snapshot — every write here
   * already replaces it wholesale, so putting the old one back is an exact
   * restore rather than an inverse move that could drift.
   */
  async move(householdId: string, entryId: string, to: MealDestination): Promise<boolean> {
    const previous = this.#week;

    if (!previous) {
      return false;
    }

    this.#week = withMealMoved(previous, entryId, to);

    const result = await request(() =>
      http.PATCH('/api/v1/households/{householdId}/meal-plan/{entryId}', {
        params: { path: { householdId, entryId } },
        body: { date: to.date, slot: to.slot, position: to.position }
      })
    );

    if (result.ok) {
      this.#week = result.value;
      this.#error = null;
    } else {
      // Exactly where it was, including the day it came from and the order of
      // the day it was dropped on.
      this.#week = previous;
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
