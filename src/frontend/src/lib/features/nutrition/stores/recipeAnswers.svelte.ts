import { http, request } from '$api';
import { registerStore } from '$shell/stores';

import { readNutrition } from '../mappers';
import type { Nutrition } from '../types';

/** What is known of one recipe: still being asked, or settled with an answer or without one (the read failed). */
export type RecipeAnswer = { settled: false } | { settled: true; nutrition: Nutrition | null };

/**
 * The nutrition of many recipes at once, for a screen that lists them (the week plan). Each recipe is asked
 * for once per household, all at the same time, and an answer arrives whenever it arrives: nothing waits for
 * the others. The HTTP client replays each recipe's ETag, so asking again after a visit is a 304, not a
 * recompute. Separate from the single-recipe store, whose one answer follows the recipe on screen.
 */
class RecipeAnswers {
  #answers = $state<Record<string, Nutrition | null>>({});

  /** Plain, not `$state`: `ensure` runs in an `$effect`, and reading what it writes would make it ask forever. */
  #asked = new Set<string>();
  #generation = 0;

  of(recipeId: string, householdId: string): RecipeAnswer {
    const key = keyOf(recipeId, householdId);

    return key in this.#answers
      ? { settled: true, nutrition: this.#answers[key] ?? null }
      : { settled: false };
  }

  /** Asks for every recipe not yet asked for, in parallel; returns when all have settled. */
  async ensure(recipeIds: readonly string[], householdId: string): Promise<void> {
    const generation = this.#generation;
    const wanted = recipeIds.filter((id) => !this.#asked.has(keyOf(id, householdId)));

    for (const id of wanted) {
      this.#asked.add(keyOf(id, householdId));
    }

    await Promise.all(
      wanted.map(async (recipeId) => {
        const result = await request(() =>
          http.GET('/api/v1/recipes/{recipeId}/nutrition', {
            params: { path: { recipeId }, query: { householdId } }
          })
        );

        if (generation === this.#generation) {
          this.#answers[keyOf(recipeId, householdId)] = result.ok
            ? readNutrition(result.value)
            : null;
        }
      })
    );
  }

  /** Forgets everything, so the next visit asks again (and a correction made meanwhile shows). */
  reset(): void {
    this.#generation += 1;
    this.#asked.clear();
    this.#answers = {};
  }
}

const keyOf = (recipeId: string, householdId: string) => `${recipeId}:${householdId}`;

export const recipeAnswers = new RecipeAnswers();

registerStore(() => recipeAnswers.reset());
