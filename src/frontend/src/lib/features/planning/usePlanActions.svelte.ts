import type { AppError } from '$api';
import { madeIt as recordMadeIt } from '$features/cooking/finishCooking';
import { lineMarks } from '$features/shopping/lineMarks.svelte';
import { listShown, openList } from '$features/shopping/openList';
import { shopping } from '$features/shopping/stores/shopping.svelte';
import { withdrawOffer } from '$features/shopping/withdrawOffer.svelte';
import { explain } from '$shell/explain';
import { m } from '$shell/i18n';
import { toaster } from '$shell/toaster.svelte';
import { preferences } from '$shell/preferences.svelte';

import {
  gapToRestore,
  mealPlan,
  placeOf,
  servingsOf,
  type MealSlot,
  type PlannedMeal,
  weekStart
} from './mealPlan.svelte';
import { upcoming } from './upcoming.svelte';
import { weekdayName } from './weekDates';
import type { Held, Landing } from './weekDrag.svelte';

interface Page {
  readonly householdId: () => string | null;
  /** Reads the days on screen again, after a change the plan's answer does not carry (the list, a cook). */
  readonly reload: () => Promise<void>;
}

/** Actions on the days on screen; each ends in a toast. */
export function usePlanActions(page: Page) {
  const ui = $state({ busy: false });

  /** Moves a meal and offers undo; a drop is cheap to reverse, a confirmation would interrupt it. */
  async function move(entryId: string, to: { date: string; slot?: MealSlot; position?: number }) {
    const householdId = page.householdId();
    const before = householdId && placeOf(mealPlan.knownDays, entryId);

    if (!householdId || !before) {
      return;
    }

    const ok = await mealPlan.move(householdId, entryId, to);

    if (!ok) {
      toaster.show({ message: () => m['plan.move.failed'](), tone: 'danger' });

      return;
    }

    // The week on screen already shows the move; a day beyond it (Sunday to next Monday) is only known to a fresh read.
    void upcoming.load(householdId);

    // From the returned week: only the server's answer can anchor an undo, as it decides a day's order.
    const after = placeOf(mealPlan.knownDays, entryId);

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

  /**
   * Saves what the edit sheet answered: the day, the meal, and for how many. Servings are for this one meal; when it is on the shopping list the
   * person says whether its share there follows (the list is a sum, so only the server can swap one meal's part of it).
   */
  async function edit(
    meal: PlannedMeal,
    to: { date: string; slot: MealSlot; servings?: number; updateList: boolean }
  ) {
    const householdId = page.householdId();
    const before = householdId && placeOf(mealPlan.knownDays, meal.entryId);

    if (!householdId || !before) {
      return;
    }

    const servingsChanged = to.servings !== undefined && to.servings !== servingsOf(meal);

    // Alone in its place unless asked elsewhere: a plain move puts a meal last in its day, and an edit of servings must not.
    const stays = to.date === before.date && to.slot === before.slot;

    if (!servingsChanged) {
      return move(meal.entryId, {
        date: to.date,
        slot: to.slot,
        ...(stays ? { position: before.index } : {})
      });
    }

    const ok = await mealPlan.move(householdId, meal.entryId, {
      date: to.date,
      slot: to.slot,
      servings: to.servings,
      ...(stays ? { position: before.index } : {})
    });

    if (!ok) {
      toaster.show({ message: () => m['plan.edit.failed'](), tone: 'danger' });

      return;
    }

    if (!meal.isOnShoppingList || !to.updateList) {
      toaster.show({ message: () => m['plan.edit.done']({ title: meal.title }), tone: 'success' });

      return;
    }

    const failure = await shopping.refreshMeal(householdId, meal.entryId);

    // The list on screen tints the amounts that changed: said there, not again in a toast.
    if (!failure && listShown()) return;

    toaster.show(
      failure
        ? { message: () => m['plan.edit.listFailed']({ title: meal.title }), tone: 'danger' }
        : { message: () => m['plan.edit.doneList']({ title: meal.title }), tone: 'success' }
    );
  }

  /**
   * One meal's shopping onto the list beside the week (dropped there, or its card's "Auf die Liste"); the meal stays
   * where it is. Already there, its lines are shown instead.
   */
  async function toList(entryId: string) {
    const householdId = page.householdId();
    const place = placeOf(mealPlan.knownDays, entryId);
    const meal =
      place && mealPlan.knownDays.find((day) => day.date === place.date)?.meals[place.index];

    if (!householdId || !place || !meal) {
      return;
    }

    if (meal.isOnShoppingList) {
      lineMarks.show(entryId);

      return;
    }

    const week = weekStart(place.date);
    const failure = await shopping.addPlannedMeal(householdId, week, entryId);

    if (failure) {
      toaster.show({ message: () => explain(failure), tone: 'danger' });

      return;
    }

    await page.reload();
  }

  /** The gaps either side of the card's own position are no move. */
  function drop(held: Held, landing: Landing) {
    if (landing.list) {
      void toList(held.entryId);

      return;
    }

    const before = placeOf(mealPlan.knownDays, held.entryId);

    if (
      before?.date === landing.date &&
      (landing.position === before.index || landing.position === before.index + 1)
    ) {
      return;
    }

    void move(held.entryId, { date: landing.date, position: landing.position });
  }

  /** Adds the shopping of each week on screen with meals still missing, a request a week: only the server knows which meals are already listed. */
  async function shop() {
    const householdId = page.householdId();

    if (!householdId) {
      return;
    }

    ui.busy = true;

    let failed: AppError | null = null;

    for (const week of mealPlan.weeks.filter((monday) => mealPlan.unshoppedIn(monday).length > 0)) {
      failed ??= await shopping.addPlannedWeek(householdId, week);
    }

    const failure = failed;

    if (!failure) {
      await page.reload();
    }

    ui.busy = false;

    toaster.show(
      failure
        ? { message: () => explain(failure), tone: 'danger' }
        : {
            message: () => m['plan.addedToList'](),
            tone: 'success',
            action: openList()
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

    // The list beside the week asks at the meal's lines, where the answer shows.
    if (listShown()) {
      withdrawOffer.offer({ entryId: meal.entryId, title: meal.title });

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

  /** "I made it" from the week: recorded, and the card says cooked once the week is read again. */
  async function madeIt(meal: PlannedMeal) {
    const householdId = page.householdId();

    if (!householdId) {
      return;
    }

    await recordMadeIt(meal.recipeId, servingsOf(meal), householdId);
    void page.reload();
  }

  return { ui, move, edit, drop, shop, unplan, madeIt, toList };
}
