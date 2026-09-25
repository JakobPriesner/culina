import { http, request } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

import { toRelated } from '../mappers';
import type { RelatedRecipe } from '../types';

/** One recipe's answer, and how far along it is. */
interface Answer {
  readonly items: readonly RelatedRecipe[];
  readonly status: LoadStatus;
}

/**
 * The recipes like each recipe that has been read.
 *
 * Kept per recipe, because going from one recipe to a related one and back is
 * the whole point of showing them, and asking again on the way back would
 * draw the shelf twice.
 */
class RelatedStore {
  #answers = $state<Record<string, Answer>>({});

  /**
   * Which recipes have been asked about.
   *
   * A plain field, not `$state`: `load` is called from an `$effect`, and a
   * guard the effect could read would make it depend on what `load` is about
   * to write, and ask forever.
   */
  #asked = new Set<string>();

  /** The recipes like this one, or none until they are in. */
  of(recipeId: string): readonly RelatedRecipe[] {
    return this.#answers[recipeId]?.items ?? [];
  }

  statusOf(recipeId: string): LoadStatus {
    return this.#answers[recipeId]?.status ?? 'idle';
  }

  /** Asks once per recipe. Calling it again for the same one does nothing. */
  async load(recipeId: string): Promise<void> {
    if (this.#asked.has(recipeId)) {
      return;
    }

    this.#asked.add(recipeId);
    this.#answers = { ...this.#answers, [recipeId]: { items: [], status: 'loading' } };

    const result = await request(() =>
      http.GET('/api/v1/recipes/{recipeId}/related', { params: { path: { recipeId } } })
    );

    this.#answers = {
      ...this.#answers,
      [recipeId]: result.ok
        ? { items: result.value.items.map(toRelated), status: 'ready' }
        : { items: [], status: 'failed' }
    };
  }

  reset(): void {
    this.#answers = {};
    this.#asked.clear();
  }
}

export const related = new RelatedStore();

registerStore(() => related.reset());
