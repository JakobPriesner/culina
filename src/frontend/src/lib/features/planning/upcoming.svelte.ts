import type { AppError } from '$api';
import { LatestRead, registerStore, type LoadStatus } from '$shell/stores';

import {
  asDate,
  cookedOn,
  mealPlan,
  readWeek,
  servingsOf,
  type MealSlot,
  type PlannedDay,
  type PlannedMeal
} from './mealPlan.svelte';
import { mondayOf } from './weekDates';

const slotOrder: readonly MealSlot[] = ['breakfast', 'lunch', 'dinner'];

/** Where a recipe is next on the plan, and which week that is from this one (for `/plan?week=`). */
export interface NextPlanned {
  /** The plan entry, for shopping with its provenance. */
  readonly entryId: string;
  /** Whether its shopping is already on the list. */
  readonly isOnShoppingList: boolean;
  /** Cooked on its day already (today's meal, made). */
  readonly cooked: boolean;
  readonly date: string;
  readonly slot: MealSlot;
  readonly week: 0 | 1;
  /** For how many it is planned. */
  readonly servings: number;
}

/**
 * The plan from today on, across this week and the next (so a Sunday still has a week ahead):
 * what the plan sheet offers, what a recipe says it is planned for, and tonight's meal.
 * Separate from `mealPlan`, which is the one week the plan page shows and must not be moved under it;
 * but where the two hold the same day, that week's copy wins, so an edit on the plan page (servings,
 * a move, a deletion, even before the server answers) is what tonight's meal shows at once.
 */
class UpcomingStore {
  #days = $state<readonly PlannedDay[]>([]);
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);

  /** Plain, not $state: read before the first await of an effect-called load. */
  #householdId: string | null = null;
  #reads = new LatestRead();

  get days(): readonly PlannedDay[] {
    if (mealPlan.householdId !== this.#householdId || mealPlan.days.length === 0) {
      return this.#days;
    }

    const edited = new Map(mealPlan.days.map((day) => [day.date, day]));

    return this.#days.map((day) => edited.get(day.date) ?? day);
  }

  get status(): LoadStatus {
    return this.#status;
  }

  get error(): AppError | null {
    return this.#error;
  }

  /** Today's meals, in the server's slot order. */
  get today(): readonly PlannedMeal[] {
    const today = asDate(new Date());

    return this.days.find((day) => day.date === today)?.meals ?? [];
  }

  /** Today's meals already cooked, so the week can say so instead of offering them again. */
  get cookedToday(): readonly PlannedMeal[] {
    const today = asDate(new Date());

    return this.today.filter((meal) => cookedOn(meal, today));
  }

  /**
   * Today's meal by the clock, not yet cooked: this part of the day's (breakfast before 11, lunch before 15, then
   * dinner), else the next still to come, else the last one left.
   */
  get mealNow(): PlannedMeal | null {
    const hour = new Date().getHours();
    const from = slotOrder.indexOf(hour < 11 ? 'breakfast' : hour < 15 ? 'lunch' : 'dinner');
    const left = this.today.filter((meal) => !this.cookedToday.includes(meal));

    return (
      left.find((meal) => slotOrder.indexOf(meal.slot as MealSlot) >= from) ?? left.at(-1) ?? null
    );
  }

  /** Every dish of the meal now (curry and rice for dinner), still to cook, in the plan's order. */
  get dishesNow(): readonly PlannedMeal[] {
    const now = this.mealNow;

    return now
      ? this.today.filter((meal) => meal.slot === now.slot && !this.cookedToday.includes(meal))
      : [];
  }

  /** The dishes planned with this recipe today, in the same meal, itself included; empty when it is not on today's plan. */
  dishesWith(recipeId: string): readonly PlannedMeal[] {
    const own = this.today.find((meal) => meal.recipeId === recipeId);

    return own ? this.today.filter((meal) => meal.slot === own.slot) : [];
  }

  nextFor(recipeId: string): NextPlanned | null {
    const nextMonday = mondayOf(1);

    for (const day of this.days) {
      const meal = day.meals.find((planned) => planned.recipeId === recipeId);

      if (meal) {
        return {
          entryId: meal.entryId,
          isOnShoppingList: meal.isOnShoppingList,
          cooked: cookedOn(meal, day.date),
          date: day.date,
          slot: meal.slot as MealSlot,
          week: day.date >= nextMonday ? 1 : 0,
          servings: servingsOf(meal)
        };
      }
    }

    return null;
  }

  /** A meal from today on, by its plan entry: what the shopping list links to at the servings it is for. */
  mealOf(entryId: string): PlannedMeal | undefined {
    return this.days.flatMap((day) => day.meals).find((meal) => meal.entryId === entryId);
  }

  async load(householdId: string): Promise<void> {
    if (this.#householdId !== householdId) {
      this.#householdId = householdId;
      this.#days = [];
    }

    const isLatest = this.#reads.start();

    this.#status = 'loading';

    const [thisWeek, nextWeek] = await Promise.all([
      readWeek(householdId, mondayOf(0)),
      readWeek(householdId, mondayOf(1))
    ]);

    if (!isLatest()) {
      return;
    }

    if (!thisWeek.ok || !nextWeek.ok) {
      this.#error = !thisWeek.ok ? thisWeek.error : nextWeek.ok ? null : nextWeek.error;
      this.#status = 'failed';

      return;
    }

    const today = asDate(new Date());

    this.#days = [...thisWeek.value.days, ...nextWeek.value.days].filter(
      (day) => day.date >= today
    );
    this.#error = null;
    this.#status = 'ready';
  }

  reset(): void {
    this.#householdId = null;
    this.#days = [];
    this.#status = 'idle';
    this.#error = null;
    this.#reads.cancel();
  }
}

export const upcoming = new UpcomingStore();

registerStore(() => upcoming.reset());
