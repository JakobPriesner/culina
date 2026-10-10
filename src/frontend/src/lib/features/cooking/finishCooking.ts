import { haptics } from '$shell/haptics';
import { cookLog } from './stores/cookLog.svelte';
import { cooking } from './stores/cooking.svelte';

export interface Finished {
  closed: boolean;
  recorded: Awaited<ReturnType<typeof cookLog.record>>;
}

/**
 * Ends the session and, if cooked, records it. Both outcomes are reported: saying it was added when
 * it never reached the server is worse than silence.
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

  const recorded = completed ? await cookLog.record(recipeId, servings, householdId) : null;

  return { closed, recorded };
}
