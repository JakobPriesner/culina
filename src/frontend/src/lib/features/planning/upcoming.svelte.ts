import type { AppError } from '$api';
import { LatestRead, registerStore, type LoadStatus } from '$shell/stores';

import {
  asDate,
  readWeek,
  type MealSlot,
  type PlannedDay,
  type PlannedMeal
} from './mealPlan.svelte';
import { mondayOf } from './weekDates';

/** Where a recipe is next on the plan, and which week that is from this one (for `/plan?week=`). */
export interface NextPlanned {
  readonly date: string;
  readonly slot: MealSlot;
  readonly week: 0 | 1;
}

/**
 * The plan from today on, across this week and the next (so a Sunday still has a week ahead):
 * what the plan sheet offers, what a recipe says it is planned for, and tonight's meal.
 * Separate from `mealPlan`, which is the one week the plan page shows and must not be moved under it.
 */
class UpcomingStore {
  #days = $state<readonly PlannedDay[]>([]);
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);

  /** Plain, not $state: read before the first await of an effect-called load. */
  #householdId: string | null = null;
  #reads = new LatestRead();

  get days(): readonly PlannedDay[] {
    return this.#days;
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

    return this.#days.find((day) => day.date === today)?.meals ?? [];
  }

  nextFor(recipeId: string): NextPlanned | null {
    const nextMonday = mondayOf(1);

    for (const day of this.#days) {
      const meal = day.meals.find((planned) => planned.recipeId === recipeId);

      if (meal) {
        return {
          date: day.date,
          slot: meal.slot as MealSlot,
          week: day.date >= nextMonday ? 1 : 0
        };
      }
    }

    return null;
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
