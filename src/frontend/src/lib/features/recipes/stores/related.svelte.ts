import { http, request } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

import { toRelated } from '../mappers';
import type { RelatedRecipe } from '../types';

interface Answer {
  /** Whose shelf, since one recipe has one per reading household. */
  readonly recipeId: string;
  readonly items: readonly RelatedRecipe[];
  readonly status: LoadStatus;
  /** Next-page cursor; null once nothing else is alike enough. */
  readonly cursor: string | null;
  /** Whether asking for the next page failed; from then on it is not asked for by itself. */
  readonly moreFailed: boolean;
}

/** One shelf per recipe and reading household: the calories on its cards are read with that household's nutrition choices. */
const keyOf = (recipeId: string, householdId: string | null) =>
  householdId ? `${recipeId}@${householdId}` : recipeId;

/** Related recipes per recipe, so going to one and back does not ask again and draw the shelf twice. */
class RelatedStore {
  #answers = $state<Record<string, Answer>>({});

  /** Plain field, not `$state`: `load` runs in an `$effect`, and a readable guard would make it depend on what it writes and loop. */
  #asked = new Set<string>();

  /** Shelves with a next page in flight; plain like {@link #asked}. */
  #fetchingMore = new Set<string>();

  of(recipeId: string, householdId: string | null = null): readonly RelatedRecipe[] {
    return this.#answers[keyOf(recipeId, householdId)]?.items ?? [];
  }

  statusOf(recipeId: string, householdId: string | null = null): LoadStatus {
    return this.#answers[keyOf(recipeId, householdId)]?.status ?? 'idle';
  }

  hasMore(recipeId: string, householdId: string | null = null): boolean {
    const answer = this.#answers[keyOf(recipeId, householdId)];

    return answer?.cursor != null && !answer.moreFailed;
  }

  /** Idempotent per recipe and household. */
  async load(recipeId: string, householdId: string | null = null): Promise<void> {
    const key = keyOf(recipeId, householdId);

    if (this.#asked.has(key)) {
      return;
    }

    this.#asked.add(key);
    this.#answers = {
      ...this.#answers,
      [key]: { recipeId, items: [], status: 'loading', cursor: null, moreFailed: false }
    };

    const result = await this.#fetch(recipeId, householdId, null);

    this.#answers = {
      ...this.#answers,
      [key]: result.ok
        ? {
            recipeId,
            items: result.value.items.map(toRelated),
            status: 'ready',
            cursor: result.value.nextCursor ?? null,
            moreFailed: false
          }
        : { recipeId, items: [], status: 'failed', cursor: null, moreFailed: false }
    };
  }

  /** Appends the next page; safe to call repeatedly while one is in flight. */
  async more(recipeId: string, householdId: string | null = null): Promise<void> {
    const key = keyOf(recipeId, householdId);
    const cursor = this.#answers[key]?.cursor;

    if (!cursor || this.#fetchingMore.has(key)) {
      return;
    }

    this.#fetchingMore.add(key);

    const result = await this.#fetch(recipeId, householdId, cursor);

    this.#fetchingMore.delete(key);

    const answer = this.#answers[key];

    // Forgotten or reset meanwhile: nothing to add it to.
    if (!answer) {
      return;
    }

    this.#answers = {
      ...this.#answers,
      [key]: result.ok
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

  #fetch(recipeId: string, householdId: string | null, cursor: string | null) {
    return request(() =>
      http.GET('/api/v1/recipes/{recipeId}/related', {
        params: {
          path: { recipeId },
          query: { cursor: cursor ?? undefined, householdId: householdId ?? undefined }
        }
      })
    );
  }

  /** Drops a deleted recipe from every shelf, since each is asked for only once. */
  forget(recipeId: string): void {
    this.#answers = Object.fromEntries(
      Object.entries(this.#answers)
        .filter(([, answer]) => answer.recipeId !== recipeId)
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
