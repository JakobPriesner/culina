import { untrack } from 'svelte';

import { http, request, type AppError } from '$api';
import { LatestRead, registerStore } from '$shell/stores';

import type { components } from '$api/generated/schema';

import { asDate, plusDays, weeksOf, weekStart } from './days';

/** A planned week, as the server reads and answers it: every write returns the whole week the meal is in. */
type Week = components['schemas']['PlanningMealPlanResponse'];

export type PlannedDay = Week['days'][number];
export type PlannedMeal = PlannedDay['meals'][number];

/** Which meal of the day. Omitted means dinner, which is what people plan. */
export type MealSlot = 'breakfast' | 'lunch' | 'dinner';

export { asDate, plusDays, weekStart, weeksOf } from './days';

export interface MealDestination {
  readonly date: string;
  /** Omitted keeps the current slot. */
  readonly slot?: MealSlot;
  /** Gap of the target day it was dropped into, counted with the moved meal still in place; omitted puts it last. */
  readonly position?: number;
  /** For how many the meal is from now on; omitted keeps what it has. */
  readonly servings?: number;
}

export interface MealPlace {
  readonly date: string;
  readonly slot: MealSlot;
  readonly index: number;
}

/** Slot order, which the server sorts a day by first. */
const slotRank = { breakfast: 0, lunch: 1, dinner: 2 } as const;

/** The week starting on the Monday of `from`, read without becoming the week on screen; the plan sheet looks ahead across two of them. */
export const readWeek = (householdId: string, from: string) =>
  request(() =>
    http.GET('/api/v1/households/{householdId}/meal-plan', {
      params: { path: { householdId }, query: { from } }
    })
  );

const rankOf = (slot: string): number => slotRank[slot as keyof typeof slotRank] ?? slotRank.dinner;

/** Whether the household cooked the meal on its day, by this device's calendar (the server cannot know its time zone). */
export const cookedOn = (meal: PlannedMeal, date: string): boolean =>
  // eslint-disable-next-line svelte/prefer-svelte-reactivity -- Read once to name its day, never changed.
  meal.cookedAt != null && asDate(new Date(meal.cookedAt)) === date;

/** For how many a meal is: what was chosen for it, or what its recipe is written for. */
export const servingsOf = (meal: PlannedMeal): number => meal.servings ?? meal.recipeServings;

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

  const moved = { ...meal, slot: to.slot ?? meal.slot, servings: to.servings ?? meal.servings };

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

/** The days on screen: from a day, for so many days, whichever weeks they fall in. */
interface Span {
  readonly from: string;
  readonly count: number;
}

class MealPlanStore {
  /** Every week read, by its Monday: the server answers in weeks, the screen shows any run of days across them. */
  #weeks = $state<Readonly<Record<string, Week>>>({});
  #span = $state<Span | null>(null);
  #error = $state<AppError | null>(null);
  /** Why the last plan, move or unplan was refused; apart from #error, so a failed write never hides the week. */
  #writeError = $state<AppError | null>(null);
  #loading = $state(false);

  /** Plain counter of move writes still in flight; a whole-week answer is only taken once none are left. */
  #inFlight = 0;

  /** Plain, not $state: read before the first await of an effect-called method, where a tracked read would retrigger on its own writes. */
  #householdId: string | null = null;

  /** Which read is the latest; an older one landing after it must not replace the days asked for since. */
  #reads = new LatestRead();

  /** The days on screen, in order. */
  get days(): readonly PlannedDay[] {
    const span = this.#span;

    if (!span) {
      return [];
    }

    const to = plusDays(span.from, span.count - 1);

    return this.knownDays.filter((day) => day.date >= span.from && day.date <= to);
  }

  /** Every day of every week read, in order: what tonight's meal and the sheets look across. */
  get knownDays(): readonly PlannedDay[] {
    return Object.keys(this.#weeks)
      .sort()
      .flatMap((monday) => this.#weeks[monday]?.days ?? []);
  }

  /** The whole weeks the days on screen fall in, which a sheet offers to move a meal within. */
  get aroundDays(): readonly PlannedDay[] {
    return this.#span ? this.weeks.flatMap((monday) => this.weekDays(monday)) : [];
  }

  /** The Mondays of the weeks the days on screen fall in: one, or two when the days cross a Sunday. */
  get weeks(): readonly string[] {
    return this.#span ? weeksOf(this.#span.from, this.#span.count) : [];
  }

  /** Whose plan is held, so a reader of other weeks can tell it is the same kitchen's. */
  get householdId(): string | null {
    return this.#householdId;
  }

  /** The first day on screen. */
  get from(): string | null {
    return this.#span?.from ?? null;
  }

  /** Why the week could not be read. */
  get error(): AppError | null {
    return this.#error;
  }

  /** Why the last change to the week was refused, for the toast that says so. */
  get writeError(): AppError | null {
    return this.#writeError;
  }

  get loading(): boolean {
    return this.#loading;
  }

  /** Every planned meal on screen, in cooking order. */
  get meals(): readonly PlannedMeal[] {
    return this.days.flatMap((day) => day.meals);
  }

  /** The seven days of a week read, or none. */
  weekDays(monday: string): readonly PlannedDay[] {
    return this.#weeks[monday]?.days ?? [];
  }

  /** Meals of a week whose ingredients are not on the shopping list yet; leftovers are bought with their original. */
  unshoppedIn(monday: string): readonly PlannedMeal[] {
    return this.weekDays(monday)
      .flatMap((day) => day.meals)
      .filter((meal) => !meal.isOnShoppingList && !meal.leftoverOf);
  }

  /**
   * Shows `count` days from `from` (a week from its Monday by default), reading the weeks they fall in. Weeks already
   * read show at once and are read again behind them, so stepping a day at a time never waits or blanks.
   */
  async load(householdId: string, from = weekStart(asDate(new Date())), count = 7): Promise<void> {
    const isLatest = this.#reads.start();
    const weeks = weeksOf(from, count);

    // Untracked: this is called from an effect, which must not come to depend on the weeks this load writes.
    untrack(() => {
      // Keep the days on screen while the next load (a skeleton loses your place), but not across households.
      if (this.#householdId !== householdId) {
        this.#householdId = householdId;
        this.#weeks = {};
        this.#span = null;
      }

      if (weeks.every((monday) => this.#weeks[monday])) {
        this.#span = { from, count };
      }

      this.#loading = true;
    });

    const results = await Promise.all(weeks.map((monday) => readWeek(householdId, monday)));

    if (!isLatest()) {
      return;
    }

    this.#loading = false;

    const failed = results.find((result) => !result.ok);

    if (failed && !failed.ok) {
      this.#error = failed.error;

      return;
    }

    for (const result of results) {
      if (result.ok) this.#take(result.value);
    }

    this.#span = { from, count };
    this.#error = null;
  }

  /** Reads the days on screen again (after a change the plan's own answer does not carry: the list, a cook). */
  refresh(householdId: string): Promise<void> {
    return this.#span
      ? this.load(householdId, this.#span.from, this.#span.count)
      : Promise.resolve();
  }

  async plan(
    householdId: string,
    meal: {
      date: string;
      recipeId: string;
      servings?: number;
      slot?: MealSlot;
      /** The planned meal of the same recipe whose rest this is. */
      leftoverOf?: string;
    }
  ): Promise<boolean> {
    const result = await request(() =>
      http.POST('/api/v1/households/{householdId}/meal-plan', {
        params: { path: { householdId } },
        body: meal
      })
    );

    if (result.ok) {
      this.#take(result.value);
      this.#writeError = null;
    } else {
      this.#writeError = result.error;
    }

    return result.ok;
  }

  /** Optimistic, since this is a drag (or an edit that is nearly always accepted). A failure moves just this meal back to where it was, so other drags meanwhile stay. */
  async move(householdId: string, entryId: string, to: MealDestination): Promise<boolean> {
    const before = placeOf(this.knownDays, entryId);

    if (!before) {
      return false;
    }

    this.#inFlight++;

    this.#edit((all) => withMealMoved(all, entryId, to));

    const result = await request(() =>
      http.PATCH('/api/v1/households/{householdId}/meal-plan/{entryId}', {
        params: { path: { householdId, entryId } },
        body: { date: to.date, slot: to.slot, position: to.position, servings: to.servings }
      })
    );

    this.#inFlight--;

    if (result.ok) {
      // Another drag still out would be undone by this week; the last answer to land carries all of them.
      if (this.#inFlight === 0) {
        this.#take(result.value);

        // The answer is the week it landed in; the week it left (Sunday to Monday) is only known to a fresh read.
        const left = weekStart(before.date);

        if (left !== result.value.from && this.#weeks[left]) {
          void this.#reread(householdId, left);
        }
      }

      this.#writeError = null;
    } else {
      this.#putBack(entryId, before);
      this.#writeError = result.error;

      // A put-back cannot say what the servings were when they were the recipe's own; the server can.
      if (to.servings !== undefined && this.#span) {
        void this.load(householdId, this.#span.from, this.#span.count);
      }
    }

    return result.ok;
  }

  #putBack(entryId: string, before: MealPlace): void {
    const after = placeOf(this.knownDays, entryId);

    if (after) {
      this.#edit((all) =>
        withMealMoved(all, entryId, {
          date: before.date,
          slot: before.slot,
          position: gapToRestore(before, after)
        })
      );
    }
  }

  async unplan(householdId: string, entryId: string): Promise<boolean> {
    const result = await request(() =>
      http.DELETE('/api/v1/households/{householdId}/meal-plan/{entryId}', {
        params: { path: { householdId, entryId } }
      })
    );

    if (result.ok) {
      this.#take(result.value);
      this.#writeError = null;
    } else {
      this.#writeError = result.error;
    }

    return result.ok;
  }

  reset(): void {
    this.#householdId = null;
    this.#weeks = {};
    this.#span = null;
    this.#error = null;
    this.#writeError = null;
    this.#loading = false;
    this.#reads.cancel();
  }

  #take(week: Week): void {
    this.#weeks = { ...this.#weeks, [week.from]: week };
  }

  /** Changes the days of every week read as one run of days, then hands each week its own back. */
  #edit(change: (all: Week) => Week): void {
    const mondays = Object.keys(this.#weeks).sort();

    if (mondays.length === 0) {
      return;
    }

    const changed = change({ from: mondays[0]!, days: [...this.knownDays] });

    this.#weeks = Object.fromEntries(
      mondays.map((monday) => [
        monday,
        {
          ...this.#weeks[monday]!,
          days: changed.days.filter((day) => weekStart(day.date) === monday)
        }
      ])
    );
  }

  async #reread(householdId: string, monday: string): Promise<void> {
    const result = await readWeek(householdId, monday);

    if (result.ok && this.#householdId === householdId) {
      this.#take(result.value);
    }
  }
}

export const mealPlan = new MealPlanStore();

registerStore(() => mealPlan.reset());
