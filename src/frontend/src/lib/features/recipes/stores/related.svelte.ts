import { http, request } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

import { toRelated } from '../mappers';
import type { RelatedRecipe } from '../types';

/** One recipe's answer, and how far along it is. */
interface Answer {
  readonly items: readonly RelatedRecipe[];
  readonly status: LoadStatus;
  /** Where the shelf goes on, or null once nothing else is alike enough. */
  readonly cursor: string | null;
  /** Whether asking for the next page failed; from then on it is not asked for by itself. */
  readonly moreFailed: boolean;
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

  /** Which shelves have a next page on its way. Plain for the same reason as {@link #asked}. */
  #fetchingMore = new Set<string>();

  /** The recipes like this one, or none until they are in. */
  of(recipeId: string): readonly RelatedRecipe[] {
    return this.#answers[recipeId]?.items ?? [];
  }

  statusOf(recipeId: string): LoadStatus {
    return this.#answers[recipeId]?.status ?? 'idle';
  }

  /** Whether the shelf goes on, and may be asked for its next page by itself. */
  hasMore(recipeId: string): boolean {
    const answer = this.#answers[recipeId];

    return answer?.cursor != null && !answer.moreFailed;
  }

  /** Asks once per recipe. Calling it again for the same one does nothing. */
  async load(recipeId: string): Promise<void> {
    if (this.#asked.has(recipeId)) {
      return;
    }

    this.#asked.add(recipeId);
    this.#answers = {
      ...this.#answers,
      [recipeId]: { items: [], status: 'loading', cursor: null, moreFailed: false }
    };

    const result = await this.#fetch(recipeId, null);

    this.#answers = {
      ...this.#answers,
      [recipeId]: result.ok
        ? {
            items: result.value.items.map(toRelated),
            status: 'ready',
            cursor: result.value.nextCursor ?? null,
            moreFailed: false
          }
        : { items: [], status: 'failed', cursor: null, moreFailed: false }
    };
  }

  /**
   * The next page of one shelf, added to the end of it.
   *
   * Free to call while a page is already on its way — the end of the shelf
   * coming into view calls it, and may do so more than once.
   */
  async more(recipeId: string): Promise<void> {
    const cursor = this.#answers[recipeId]?.cursor;

    if (!cursor || this.#fetchingMore.has(recipeId)) {
      return;
    }

    this.#fetchingMore.add(recipeId);

    const result = await this.#fetch(recipeId, cursor);

    this.#fetchingMore.delete(recipeId);

    const answer = this.#answers[recipeId];

    // Forgotten or reset while the page was on its way: there is nothing
    // left to add it to.
    if (!answer) {
      return;
    }

    this.#answers = {
      ...this.#answers,
      [recipeId]: result.ok
        ? {
            ...answer,
            // A recipe the kitchen changed under the shelf can come round
            // twice; the one already showing stays where it is.
            items: [
              ...answer.items,
              ...result.value.items
                .map(toRelated)
                .filter((item) => !answer.items.some((shown) => shown.id === item.id))
            ],
            cursor: result.value.nextCursor ?? null
          }
        : { ...answer, moreFailed: true }
    };
  }

  #fetch(recipeId: string, cursor: string | null) {
    return request(() =>
      http.GET('/api/v1/recipes/{recipeId}/related', {
        params: { path: { recipeId }, query: { cursor: cursor ?? undefined } }
      })
    );
  }

  /**
   * Takes a deleted recipe off every shelf it was on.
   *
   * Each recipe's shelf is asked for once, so without this the recipe next
   * door would go on showing one that opens onto nothing.
   */
  forget(recipeId: string): void {
    this.#answers = Object.fromEntries(
      Object.entries(this.#answers)
        .filter(([key]) => key !== recipeId)
        .map(([key, answer]) => [
          key,
          { ...answer, items: answer.items.filter((item) => item.id !== recipeId) }
        ])
    );
  }

  reset(): void {
    this.#answers = {};
    this.#asked.clear();
    this.#fetchingMore.clear();
  }
}

export const related = new RelatedStore();

registerStore(() => related.reset());
