import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

import type { components } from '$api/generated/schema';

/** The planned week (not a calendar). Every write returns the whole week, since the week is the screen. */
type Week = components['schemas']['PlanningMealPlanResponse'];

export type PlannedDay = Week['days'][number];
export type PlannedMeal = PlannedDay['meals'][number];

/** Which meal of the day. Omitted means dinner, which is what people plan. */
export type MealSlot = 'breakfast' | 'lunch' | 'dinner';

/** The date as the API spells it (YYYY-MM-DD). */
export const asDate = (day: Date): string =>
  `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`;

export interface MealDestination {
  readonly date: string;
  /** Omitted keeps the current slot. */
  readonly slot?: MealSlot;
  /** Gap of the target day it was dropped into, counted with the moved meal still in place; omitted puts it last. */
  readonly position?: number;
}

export interface MealPlace {
  readonly date: string;
  readonly slot: MealSlot;
  readonly index: number;
}

/** Slot order, which the server sorts a day by first. */
const slotRank = { breakfast: 0, lunch: 1, dinner: 2 } as const;

const rankOf = (slot: string): number => slotRank[slot as keyof typeof slotRank] ?? slotRank.dinner;

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

/** The gap that puts a meal back: counted with the meal still in the day, so undoing a same-day move down needs index + 1. */
export const gapToRestore = (before: MealPlace, after: MealPlace): number =>
  before.date === after.date && after.index < before.index ? before.index + 1 : before.index;

/** The week with one meal moved; mirrors the server's slot-first order so the meal doesn't jump when the response lands. */
export const withMealMoved = (week: Week, entryId: string, to: MealDestination): Week => {
  const from = placeOf(week.days, entryId);
  const meal = from && week.days.find((day) => day.date === from.date)?.meals[from.index];

  if (!from || !meal) {
    return week;
  }

  const moved = { ...meal, slot: to.slot ?? meal.slot };

  // Counted against the day as on screen, which still holds the meal when it is the source day.
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

  /** Plain counter of move writes still in flight; a whole-week answer is only taken once none are left. */
  #inFlight = 0;

  /** Plain, not $state: read before the first await of an effect-called method, where a tracked read would retrigger on its own writes. */
  #householdId: string | null = null;

  get days(): readonly PlannedDay[] {
    return this.#week?.days ?? [];
  }

  /** Monday the week on screen starts on. */
  get from(): string | null {
    return this.#week?.from ?? null;
  }

  get error(): AppError | null {
    return this.#error;
  }

  get loading(): boolean {
    return this.#loading;
  }

  /** Every planned meal of the week, in cooking order. */
  get meals(): readonly PlannedMeal[] {
    return this.days.flatMap((day) => day.meals);
  }

  /** Meals whose ingredients are not on the shopping list yet. */
  get unshopped(): readonly PlannedMeal[] {
    return this.meals.filter((meal) => !meal.isOnShoppingList);
  }

  async load(householdId: string, from?: string): Promise<void> {
    // Keep the week on screen while the next loads (a skeleton loses your place), but not across households.
    if (this.#householdId !== householdId) {
      this.#householdId = householdId;
      this.#week = null;
    }

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

  /** Optimistic, since this is a drag. A failure moves just this meal back to where it was, so other drags meanwhile stay. */
  async move(householdId: string, entryId: string, to: MealDestination): Promise<boolean> {
    const before = this.#week && placeOf(this.#week.days, entryId);

    if (!this.#week || !before) {
      return false;
    }

    this.#inFlight++;

    this.#week = withMealMoved(this.#week, entryId, to);

    const result = await request(() =>
      http.PATCH('/api/v1/households/{householdId}/meal-plan/{entryId}', {
        params: { path: { householdId, entryId } },
        body: { date: to.date, slot: to.slot, position: to.position }
      })
    );

    this.#inFlight--;

    if (result.ok) {
      // Another drag still out would be undone by this week; the last answer to land carries all of them.
      if (this.#inFlight === 0) {
        this.#week = result.value;
      }

      this.#error = null;
    } else {
      this.#putBack(entryId, before);
      this.#error = result.error;
    }

    return result.ok;
  }

  #putBack(entryId: string, before: MealPlace): void {
    const after = this.#week && placeOf(this.#week.days, entryId);

    if (this.#week && after) {
      this.#week = withMealMoved(this.#week, entryId, {
        date: before.date,
        slot: before.slot,
        position: gapToRestore(before, after)
      });
    }
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
    this.#householdId = null;
    this.#week = null;
    this.#error = null;
    this.#loading = false;
  }
}

export const mealPlan = new MealPlanStore();

registerStore(() => mealPlan.reset());
