import { resolve } from '$app/paths';
import { shopping } from '$features/shopping/stores/shopping.svelte';
import { explain } from '$shell/explain';
import { m } from '$shell/i18n';
import { toaster } from '$shell/toaster.svelte';
import { preferences } from '$shell/preferences.svelte';

import {
  gapToRestore,
  mealPlan,
  placeOf,
  type MealSlot,
  type PlannedMeal
} from './mealPlan.svelte';
import { weekdayName } from './weekDates';
import type { Held, Landing } from './weekDrag.svelte';

interface Page {
  readonly householdId: () => string | null;
  readonly monday: () => string;
}

const goToList = async () => {
  const { goto } = await import('$app/navigation');

  await goto(resolve('/(app)/shopping'));
};

/** Actions on the week on screen; each ends in a toast. */
export function usePlanActions(page: Page) {
  const ui = $state({ busy: false });

  /** Moves a meal and offers undo; a drop is cheap to reverse, a confirmation would interrupt it. */
  async function move(entryId: string, to: { date: string; slot?: MealSlot; position?: number }) {
    const householdId = page.householdId();
    const before = householdId && placeOf(mealPlan.days, entryId);

    if (!householdId || !before) {
      return;
    }

    const ok = await mealPlan.move(householdId, entryId, to);

    if (!ok) {
      toaster.show({ message: () => m['plan.move.failed'](), tone: 'danger' });

      return;
    }

    // From the returned week: only the server's answer can anchor an undo, as it decides a day's order.
    const after = placeOf(mealPlan.days, entryId);

    toaster.show({
      message: () => m['plan.move.done']({ day: weekdayName(to.date, preferences.locale) }),
      action: after
        ? {
            label: () => m['plan.move.undo'](),
            run: () =>
              void move(entryId, {
                date: before.date,
                slot: before.slot,
                position: gapToRestore(before, after)
              })
          }
        : undefined
    });
  }

  /** The gaps either side of the card's own position are no move. */
  function drop(held: Held, landing: Landing) {
    const before = placeOf(mealPlan.days, held.entryId);

    if (
      before?.date === landing.date &&
      (landing.position === before.index || landing.position === before.index + 1)
    ) {
      return;
    }

    void move(held.entryId, { date: landing.date, position: landing.position });
  }

  /** Adds the week's shopping in one request: only the server knows which meals are already listed. */
  async function shop() {
    const householdId = page.householdId();

    if (!householdId) {
      return;
    }

    ui.busy = true;

    const week = mealPlan.from ?? page.monday();
    const failure = await shopping.addPlannedWeek(householdId, week);

    if (!failure) {
      await mealPlan.load(householdId, week);
    }

    ui.busy = false;

    toaster.show(
      failure
        ? { message: () => explain(failure), tone: 'danger' }
        : {
            message: () => m['plan.addedToList'](),
            tone: 'success',
            action: { label: () => m['plan.openList'](), run: () => void goToList() }
          }
    );
  }

  async function withdraw(entryId: string) {
    const householdId = page.householdId();

    if (!householdId) {
      return;
    }

    const failure = await shopping.withdrawMeal(householdId, entryId);

    toaster.show(
      failure
        ? { message: () => explain(failure), tone: 'danger' }
        : { message: () => m['plan.unplanned.withdrawn'](), tone: 'success' }
    );
  }

  /** Unplans a meal and offers (not forces) removing its shopping, which may already be in the cupboard. */
  async function unplan(meal: PlannedMeal) {
    const householdId = page.householdId();

    if (!householdId) {
      return;
    }

    const ok = await mealPlan.unplan(householdId, meal.entryId);

    if (!ok || !meal.isOnShoppingList) {
      return;
    }

    toaster.show({
      message: () => m['plan.unplanned.stillOnList']({ title: meal.title }),
      action: {
        label: () => m['plan.unplanned.withdraw'](),
        run: () => void withdraw(meal.entryId)
      }
    });
  }

  return { ui, move, drop, shop, unplan };
}
