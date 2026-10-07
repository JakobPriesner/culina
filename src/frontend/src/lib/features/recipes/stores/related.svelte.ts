import { http, request } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

import { toRelated } from '../mappers';
import type { RelatedRecipe } from '../types';

interface Answer {
  readonly items: readonly RelatedRecipe[];
  readonly status: LoadStatus;
  /** Next-page cursor; null once nothing else is alike enough. */
  readonly cursor: string | null;
  /** Whether asking for the next page failed; from then on it is not asked for by itself. */
  readonly moreFailed: boolean;
}

/** Related recipes per recipe, so going to one and back does not ask again and draw the shelf twice. */
class RelatedStore {
  #answers = $state<Record<string, Answer>>({});

  /** Plain field, not `$state`: `load` runs in an `$effect`, and a readable guard would make it depend on what it writes and loop. */
  #asked = new Set<string>();

  /** Shelves with a next page in flight; plain like {@link #asked}. */
  #fetchingMore = new Set<string>();

  of(recipeId: string): readonly RelatedRecipe[] {
    return this.#answers[recipeId]?.items ?? [];
  }

  statusOf(recipeId: string): LoadStatus {
    return this.#answers[recipeId]?.status ?? 'idle';
  }

  hasMore(recipeId: string): boolean {
    const answer = this.#answers[recipeId];

    return answer?.cursor != null && !answer.moreFailed;
  }

  /** Idempotent per recipe. */
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

  /** Appends the next page; safe to call repeatedly while one is in flight. */
  async more(recipeId: string): Promise<void> {
    const cursor = this.#answers[recipeId]?.cursor;

    if (!cursor || this.#fetchingMore.has(recipeId)) {
      return;
    }

    this.#fetchingMore.add(recipeId);

    const result = await this.#fetch(recipeId, cursor);

    this.#fetchingMore.delete(recipeId);

    const answer = this.#answers[recipeId];

    // Forgotten or reset meanwhile: nothing to add it to.
    if (!answer) {
      return;
    }

    this.#answers = {
      ...this.#answers,
      [recipeId]: result.ok
        ? {
            ...answer,
            // A recipe changed under the shelf can come round twice; keep the one already showing.
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

  /** Drops a deleted recipe from every shelf, since each is asked for only once. */
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
