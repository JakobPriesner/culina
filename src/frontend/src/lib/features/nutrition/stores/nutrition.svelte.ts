import { http, request } from '$api';
import { LatestRead, registerStore, type LoadStatus } from '$shell/stores';

import { toNutrition } from '../mappers';
import type { Nutrition } from '../types';

/**
 * The nutrition of the recipe on screen, for the household reading it.
 * An answer already seen is shown at once while the server is asked again (a 304 when nothing changed, thanks to the
 * ETag), so going back to a recipe never shows a skeleton, and an edit is picked up.
 */
class NutritionStore {
  #key = $state<string | null>(null);
  #answer = $state<Nutrition | null>(null);
  #status = $state<LoadStatus>('idle');

  /** Plain fields, not `$state`: `load` runs in an `$effect`, and reading what it writes would make it ask forever. */
  #seen = new Map<string, Nutrition>();
  #latest = new LatestRead();

  /** What was read for exactly this recipe and household; nothing while it is another's, so a stale answer never flashes. */
  answerFor(recipeId: string, householdId: string | null): Nutrition | null {
    return this.#key === keyOf(recipeId, householdId) ? this.#answer : null;
  }

  statusFor(recipeId: string, householdId: string | null): LoadStatus {
    return this.#key === keyOf(recipeId, householdId) ? this.#status : 'idle';
  }

  async load(recipeId: string, householdId: string | null): Promise<void> {
    const key = keyOf(recipeId, householdId);
    const current = this.#latest.start();
    const known = this.#seen.get(key) ?? null;

    this.#key = key;
    this.#answer = known;
    this.#status = known ? 'ready' : 'loading';

    const result = await request(() =>
      http.GET('/api/v1/recipes/{recipeId}/nutrition', {
        params: { path: { recipeId }, query: { householdId: householdId ?? undefined } }
      })
    );

    if (!current()) {
      return;
    }

    if (!result.ok) {
      // What is on screen was true a moment ago; only say nothing is available when there is nothing.
      if (!known) {
        this.#status = 'failed';
      }

      return;
    }

    const answer = toNutrition(result.value);

    this.#seen.set(key, answer);
    this.#answer = answer;
    this.#status = 'ready';
  }

  reset(): void {
    this.#latest.cancel();
    this.#seen.clear();
    this.#key = null;
    this.#answer = null;
    this.#status = 'idle';
  }
}

const keyOf = (recipeId: string, householdId: string | null) => `${recipeId}:${householdId ?? ''}`;

export const nutrition = new NutritionStore();

registerStore(() => nutrition.reset());
