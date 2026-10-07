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

/** What the actions need to know about the page they are on. */
interface Page {
  readonly householdId: () => string | null;
  /** The Monday on screen. */
  readonly monday: () => string;
}

const goToList = async () => {
  const { goto } = await import('$app/navigation');

  await goto(resolve('/(app)/shopping'));
};

/**
 * What can be done to the week on screen: moving a meal, taking one off, and
 * putting the week's shopping on the list. Every one ends in a toast, so the
 * page is left with markup and with the week it is showing.
 */
export function usePlanActions(page: Page) {
  /** Whether the week's shopping is on its way to the list. */
  const ui = $state({ busy: false });

  /**
   * Puts a meal on another day, and offers to put it back.
   *
   * Undo rather than a confirmation: a drop is cheap to reverse and expensive
   * to interrupt, and the drop that lands a day out is common enough on a phone
   * that the way back has to be on the screen it lands on.
   */
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

    // Where it landed, read from the week that came back rather than from the
    // one that was on screen: the server decides the order of a day, so its
    // answer is the only one an undo can be built against.
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

  /** A card let go over a day. The gaps either side of where it was are no move. */
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

  /**
   * Puts the week's shopping on the list, each meal once.
   *
   * One request, because only the server can see which meals are already on
   * the list — and pressing this twice, or after adding one of the recipes from
   * its own page, must not buy anything twice. The week is read again after, so
   * every card says truthfully which meals are on the list now.
   */
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

  /**
   * Takes a meal off the plan, and offers to take its shopping off too.
   *
   * Offered rather than done: the ingredients may already be in a cupboard,
   * or wanted for something else, and a list that emptied itself behind
   * somebody's back would be as untrustworthy as one that doubled.
   */
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
