import { http, request, type AppError } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

import { toSuggestion } from '../mappers';
import type { Suggestion } from '../types';

import { LruCache } from './lruCache.svelte';
import {
  AnswersKept,
  DefaultLimit,
  keyOf,
  Most,
  type Answer,
  type SuggestionQuery
} from './suggestionQuery';

export type { SuggestionQuery };

/** A bounded, reasoned set of what to cook for one occasion, never a feed. */
class SuggestionStore {
  /**
   * One entry per question (LRU past {@link AnswersKept}); evicting also un-asks, so coming back
   * asks again.
   */
  #answers = new LruCache<Answer>(AnswersKept, (key) => this.#asked.delete(key));
  #error = $state<AppError | null>(null);

  /**
   * Not `$state` on purpose: `ask` runs in an `$effect`, so a reactive guard would re-trigger
   * itself and flood requests until a 429.
   */
  #asked = new Set<string>();

  #fetchingMore = new Set<string>();

  get error(): AppError | null {
    return this.#error;
  }

  for(householdId: string | null, query: SuggestionQuery = {}): readonly Suggestion[] {
    return householdId ? (this.#answers.get(keyOf(householdId, query))?.items ?? []) : [];
  }

  statusOf(householdId: string | null, query: SuggestionQuery = {}): LoadStatus {
    return householdId ? (this.#answers.get(keyOf(householdId, query))?.status ?? 'idle') : 'idle';
  }

  /** Whether the question has come back (ready or failed), so the page isn't rearranged twice. */
  answered(householdId: string | null, query: SuggestionQuery = {}): boolean {
    const status = this.statusOf(householdId, query);

    return status === 'ready' || status === 'failed';
  }

  hasMore(householdId: string | null, query: SuggestionQuery = {}): boolean {
    return householdId ? (this.#answers.get(keyOf(householdId, query))?.more ?? false) : false;
  }

  /** The next few: the same question excluding what is shown. */
  async more(householdId: string, query: SuggestionQuery = {}): Promise<void> {
    const key = keyOf(householdId, query);
    const shown = this.#answers.peek(key);

    if (!shown?.more || this.#fetchingMore.has(key)) {
      return;
    }

    this.#fetchingMore.add(key);

    const size = query.limit ?? DefaultLimit;
    const result = await this.#request(householdId, query, [
      ...(query.exclude ?? []),
      ...shown.items.map((item) => item.id)
    ]);

    this.#fetchingMore.delete(key);

    const answer = this.#answers.peek(key);

    if (!answer) {
      return;
    }

    const next = result.ok
      ? result.value.items
          .map(toSuggestion)
          .filter((item) => !answer.items.some((one) => one.id === item.id))
      : [];
    const items = [...answer.items, ...next];

    this.#answers.set(key, {
      ...answer,
      items,
      more: result.ok && result.value.items.length >= size && items.length < Most
    });
  }

  async ask(householdId: string, query: SuggestionQuery = {}): Promise<void> {
    const key = keyOf(householdId, query);

    if (this.#asked.has(key)) {
      return;
    }

    this.#asked.add(key);

    await this.#fetch(householdId, query, key);
  }

  async retry(householdId: string, query: SuggestionQuery = {}): Promise<void> {
    await this.#fetch(householdId, query, keyOf(householdId, query));
  }

  /** Stops suggesting a recipe everywhere, optimistically; every cached answer is filtered. */
  async dismiss(recipeId: string): Promise<AppError | null> {
    const before = this.#answers.snapshot();

    this.#without(recipeId);

    const result = await request(() =>
      http.PUT('/api/v1/recipes/{recipeId}/suggestion-dismissal', {
        params: { path: { recipeId } }
      })
    );

    if (!result.ok) {
      this.#answers.restore(before);

      return result.error;
    }

    return null;
  }

  async restore(recipeId: string): Promise<AppError | null> {
    const result = await request(() =>
      http.DELETE('/api/v1/recipes/{recipeId}/suggestion-dismissal', {
        params: { path: { recipeId } }
      })
    );

    if (!result.ok) {
      return result.error;
    }

    // Not patched back: a restored recipe reappears on the next ask, wherever the ranking puts it.
    this.#asked.clear();

    return null;
  }

  forget(recipeId: string): void {
    this.#without(recipeId);
  }

  #without(recipeId: string): void {
    this.#answers.update((answer) => ({
      ...answer,
      items: answer.items.filter((item) => item.id !== recipeId)
    }));
  }

  reset(): void {
    this.#answers.clear();
    this.#error = null;
    this.#asked.clear();
    this.#fetchingMore.clear();
  }

  async #fetch(householdId: string, query: SuggestionQuery, key: string): Promise<void> {
    const items = this.#answers.peek(key)?.items ?? [];

    this.#answers.set(key, { items, status: 'loading', more: false });
    this.#error = null;

    const result = await this.#request(householdId, query, query.exclude);

    if (result.ok) {
      this.#answers.set(key, {
        items: result.value.items.map(toSuggestion),
        status: 'ready',
        more: result.value.items.length >= (query.limit ?? DefaultLimit)
      });

      return;
    }

    this.#error = result.error;
    this.#answers.set(key, {
      items: this.#answers.peek(key)?.items ?? [],
      status: 'failed',
      more: false
    });
  }

  #request(householdId: string, query: SuggestionQuery, exclude: readonly string[] | undefined) {
    return request(() =>
      http.GET('/api/v1/suggestions', {
        params: {
          query: {
            householdId,
            slot: query.slot,
            maxMinutes: query.maxMinutes,
            exclude: exclude ? [...exclude] : undefined,
            limit: query.limit
          }
        }
      })
    );
  }
}

export const suggestions = new SuggestionStore();

registerStore(() => suggestions.reset());
