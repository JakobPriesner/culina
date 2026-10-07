import { haptics } from '$shell/haptics';
import { kitchenTimers as timers } from './kitchen.svelte';
import { cookLog } from './stores/cookLog.svelte';
import { cooking } from './stores/cooking.svelte';

export interface Finished {
  /** Whether the session was ended on the server. */
  closed: boolean;
  /** The history entry for "I made it"; null when it was not recorded. */
  recorded: Awaited<ReturnType<typeof cookLog.record>>;
}

/**
 * Ends the cooking session, and when the recipe was actually cooked, records it.
 *
 * Both halves of "I made it" are reported, not just the one that happens to
 * have a toast: saying it was added when the attempt never reached the server,
 * or when the session it belongs to is still open, is worse than saying
 * nothing — the history is the only place anyone would go to check.
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

  const closed = await cooking.end(completed);
  timers.clear();

  const recorded = completed ? await cookLog.record(recipeId, servings, householdId) : null;

  return { closed, recorded };
}
