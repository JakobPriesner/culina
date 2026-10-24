import { offerPortion, takeBackPortion } from '$features/diary/logging';
import { upcoming } from '$features/planning/upcoming.svelte';
import { haptics } from '$shell/haptics';
import { m } from '$shell/i18n';
import { toaster } from '$shell/toaster.svelte';
import { cookedArt } from './CookedArt.svelte';
import { cookLog } from './stores/cookLog.svelte';
import { cooking } from './stores/cooking.svelte';

export interface Finished {
  closed: boolean;
  recorded: Awaited<ReturnType<typeof cookLog.record>>;
}

/**
 * Ends this recipe's session and, if cooked, records it. Both outcomes are reported: saying it was
 * added when it never reached the server is worse than silence. A session for another recipe stays
 * on the hob: the side dish is done, the main still cooking.
 */
export async function finishCooking(
  recipeId: string,
  servings: number,
  householdId: string | null,
  completed: boolean
): Promise<Finished> {
  if (completed) {
    haptics.celebrate();
  }

  const onTheHob = cooking.session !== null && cooking.session.recipeId !== recipeId;
  const closed = onTheHob ? true : await cooking.end(completed);

  const recorded = completed ? await cookLog.record(recipeId, servings, householdId) : null;

  // The week shows a meal cooked once it is; read it again rather than guess which meal this was.
  if (recorded && householdId) {
    void upcoming.load(householdId);
  }

  // For someone who keeps a food diary: their portion of it, one more tap away, from cook mode or "made it" alike.
  if (recorded) {
    offerPortion(recipeId, householdId, recorded.entryId);
  }

  return { closed, recorded };
}

/**
 * "I made it", from wherever it was said (the end of cook mode, the recipe, tonight's meal, the
 * week): records it, ends its session, and says so with an undo, since one tap is cheap to take back.
 * The recipe page then offers its wrap-up (a photo, a note, the leftover shopping).
 */
export async function madeIt(
  recipeId: string,
  servings: number,
  householdId: string | null
): Promise<Finished> {
  const finished = await finishCooking(recipeId, servings, householdId, true);
  const { closed, recorded } = finished;

  toaster.show(
    recorded && closed
      ? {
          message: () => m['cooking.madeIt.toast'](),
          tone: 'success',
          art: cookedArt,
          action: {
            label: () => m['cooking.madeIt.undo'](),
            run: () => void undoMadeIt(recipeId, recorded.entryId, householdId)
          }
        }
      : { message: () => m['cooking.madeIt.failed'](), tone: 'danger' }
  );

  return finished;
}

async function undoMadeIt(recipeId: string, entryId: string, householdId: string | null) {
  await cookLog.undo(recipeId, entryId);
  await takeBackPortion(entryId);

  if (householdId) {
    void upcoming.load(householdId);
  }
}
