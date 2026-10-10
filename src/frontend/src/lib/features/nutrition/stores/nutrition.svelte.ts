import { http, request, type AppError } from '$api';
import { LatestRead, registerStore, type LoadStatus } from '$shell/stores';

import { readNutrition } from '../mappers';
import type { Correction, Nutrition, NutritionLine } from '../types';

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

  /** The same for the recipe a share link names; nobody's corrections apply to it. */
  answerForShared(token: string): Nutrition | null {
    return this.answerFor(sharedKeyOf(token), null);
  }

  statusForShared(token: string): LoadStatus {
    return this.statusFor(sharedKeyOf(token), null);
  }

  load(recipeId: string, householdId: string | null): Promise<void> {
    return this.#read(keyOf(recipeId, householdId), () =>
      http.GET('/api/v1/recipes/{recipeId}/nutrition', {
        params: { path: { recipeId }, query: { householdId: householdId ?? undefined } }
      })
    );
  }

  /** Signed out is fine: the token is the whole authorisation. */
  loadShared(token: string): Promise<void> {
    return this.#read(keyOf(sharedKeyOf(token), null), () =>
      http.GET('/api/v1/shared-recipes/{token}/nutrition', { params: { path: { token } } })
    );
  }

  async #read(key: string, ask: Ask): Promise<void> {
    const current = this.#latest.start();
    const known = this.#seen.get(key) ?? null;

    this.#key = key;
    this.#answer = known;
    this.#status = known ? 'ready' : 'loading';

    const result = await request(ask);

    if (!current()) {
      return;
    }

    // An answer this client cannot read is a failed read, like any other.
    const answer = result.ok ? readNutrition(result.value) : null;

    if (!answer) {
      // What is on screen was true a moment ago; only say nothing is available when there is nothing.
      if (!known) {
        this.#status = 'failed';
      }

      return;
    }

    this.#seen.set(key, answer);
    this.#answer = answer;
    this.#status = 'ready';
  }

  /**
   * Says what an ingredient name is, for every recipe of the household. The rows change at once and the
   * server is told; when it refuses, the rows go back to what they were. On success the answer is read
   * again, since its totals are the server's to give (its ETag changed with the correction).
   */
  async correct(
    recipeId: string,
    householdId: string,
    name: string,
    ingredientIds: readonly string[],
    correction: Correction
  ): Promise<AppError | null> {
    const key = keyOf(recipeId, householdId);
    const snapshot = this.#answer;

    if (this.#key === key && snapshot) {
      this.#answer = {
        ...snapshot,
        ingredients: snapshot.ingredients.map((line) =>
          ingredientIds.includes(line.ingredientId) ? corrected(line, correction) : line
        )
      };
    }

    const path = { householdId, name };
    const result = await request(() =>
      correction.kind === 'default'
        ? http.DELETE('/api/v1/households/{householdId}/ingredients/{name}', { params: { path } })
        : http.PUT('/api/v1/households/{householdId}/ingredients/{name}', {
            params: { path },
            body: { food: correction.kind === 'food' ? correction.food.code : null }
          })
    );

    if (!result.ok) {
      if (this.#key === key) {
        this.#answer = snapshot;
      }

      return result.error;
    }

    await this.load(recipeId, householdId);

    return null;
  }

  reset(): void {
    this.#latest.cancel();
    this.#seen.clear();
    this.#key = null;
    this.#answer = null;
    this.#status = 'idle';
  }
}

type Ask = Parameters<typeof request<Parameters<typeof readNutrition>[0]>>[0];

/** The line as the correction will make it; the server's reading replaces this a moment later. */
const corrected = (line: NutritionLine, correction: Correction): NutritionLine => {
  switch (correction.kind) {
    case 'food':
      return {
        ...line,
        status: line.status === 'excluded' ? 'counted' : line.status,
        food: correction.food,
        corrected: true
      };
    case 'exclude':
      return {
        ...line,
        status: 'excluded',
        food: null,
        grams: null,
        via: null,
        energyKcal: null,
        corrected: true
      };
    case 'default':
      return { ...line, corrected: false };
  }
};

const sharedKeyOf = (token: string) => `shared:${token}`;

const keyOf = (recipeId: string, householdId: string | null) => `${recipeId}:${householdId ?? ''}`;

export const nutrition = new NutritionStore();

registerStore(() => nutrition.reset());
