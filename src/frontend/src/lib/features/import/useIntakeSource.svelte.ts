import { untrack } from 'svelte';

import { intakes, type IntakeJob } from './intakes.svelte';

interface Recipe {
  readonly recipeId: () => string;
  /** The import named in the address (`?intake=`), when the editor was opened from it. */
  readonly intakeId: () => string | null;
}

/**
 * The import a recipe was made from, for the editor to show beside it. Opening the editor on a ready
 * import is the review, so it is marked reviewed then and the banner leading here goes away.
 */
export function useIntakeSource(on: Recipe) {
  /** Kept once found: marking it reviewed takes it out of the list it was found in. */
  let job = $state<IntakeJob | null>(null);
  /** Plain fields, not $state: read inside the effect, they must not wake it again. */
  let fetched: string | null = null;
  let marked: string | null = null;

  const listed = $derived.by(() => {
    const recipeId = on.recipeId();
    const intakeId = on.intakeId();

    return (
      intakes.jobs.find((one) =>
        intakeId ? one.id === intakeId : one.recipeId === recipeId && one.stage === 'ready'
      ) ?? null
    );
  });

  $effect(() => {
    const found = listed;
    const intakeId = on.intakeId();

    untrack(() => {
      if (found) {
        adopt(found);
      } else if (intakeId && fetched !== intakeId) {
        fetched = intakeId;
        void intakes.get(intakeId);
      }
    });
  });

  function adopt(found: IntakeJob) {
    // Only the import this recipe came from: an address naming another recipe's import shows nothing.
    if (found.recipeId !== on.recipeId()) return;

    job = found;

    if (found.stage === 'ready' && marked !== found.id) {
      marked = found.id;
      void intakes.reviewed(found.id);
    }
  }

  return {
    get job() {
      return job;
    }
  };
}
