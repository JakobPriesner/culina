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

/**
 * What this person might want to cook, for one occasion.
 *
 * A bounded set with a reason for each, never a feed. Ranking the whole
 * collection is the recipe store's job with `sort: 'suggested'` — a cookbook is
 * a view of the library rather than a second one, and so is a suggestion.
 */
class SuggestionStore {
  /**
   * One entry per question, the least recently used forgotten past
   * {@link AnswersKept}.
   *
   * Status is per question rather than one flag for the store, because two
   * occasions are regularly in flight at once — the panel at the top of the
   * library and the plan's picker — and a shared flag would have each of them
   * reporting the other's progress.
   *
   * A forgotten question is also forgotten as asked, so coming back to it
   * asks again rather than showing nothing for good.
   */
  #answers = new LruCache<Answer>(AnswersKept, (key) => this.#asked.delete(key));
  #error = $state<AppError | null>(null);

  /**
   * Which questions have already been asked.
   *
   * Deliberately **not** `$state`, and this is the trap the whole file exists to
   * avoid. `ask` is called from an `$effect`; an effect tracks every reactive
   * value read while it runs, so a guard that lived in `$state` would make `ask`
   * depend on the very thing it is about to write and re-trigger itself. It does
   * not present as a hang — it presents as a flood of identical requests and
   * then a 429, because the session rate limiter starts refusing them.
   *
   * A plain field means "asked at most once per question, full stop". Retrying
   * is a separate, deliberate call, so a failure can never silently re-arm it.
   */
  #asked = new Set<string>();

  /** Which questions have their next page on its way. Plain for the same reason as {@link #asked}. */
  #fetchingMore = new Set<string>();

  get error(): AppError | null {
    return this.#error;
  }

  /** The answer to one question, or nothing until it has been asked. */
  for(householdId: string | null, query: SuggestionQuery = {}): readonly Suggestion[] {
    return householdId ? (this.#answers.get(keyOf(householdId, query))?.items ?? []) : [];
  }

  statusOf(householdId: string | null, query: SuggestionQuery = {}): LoadStatus {
    return householdId ? (this.#answers.get(keyOf(householdId, query))?.status ?? 'idle') : 'idle';
  }

  /**
   * Whether this question has come back at all, successfully or not.
   *
   * A caller that changes what it shows depending on the answer needs to know
   * when there is going to be one. Deciding on an empty answer and again on the
   * real one means rearranging the page under somebody who has already started
   * reading it.
   */
  answered(householdId: string | null, query: SuggestionQuery = {}): boolean {
    const status = this.statusOf(householdId, query);

    return status === 'ready' || status === 'failed';
  }

  /** Whether the answer goes on past what is shown. */
  hasMore(householdId: string | null, query: SuggestionQuery = {}): boolean {
    return householdId ? (this.#answers.get(keyOf(householdId, query))?.more ?? false) : false;
  }

  /**
   * The next few, after the ones already shown.
   *
   * Not a cursor: the suggestions are not paged, but they answer by the day,
   * so the same question with everything already shown excluded is exactly
   * the rest of today's list. Free to call while a page is on its way.
   *
   * A failure ends the list rather than being reported. What is already
   * shown is still right, and the shortlist was complete without the rest.
   */
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

  /** Asks a question once. Calling it again with the same one does nothing. */
  async ask(householdId: string, query: SuggestionQuery = {}): Promise<void> {
    const key = keyOf(householdId, query);

    if (this.#asked.has(key)) {
      return;
    }

    this.#asked.add(key);

    await this.#fetch(householdId, query, key);
  }

  /** Asks again after a failure. Separate from {@link ask} so a retry is a decision. */
  async retry(householdId: string, query: SuggestionQuery = {}): Promise<void> {
    await this.#fetch(householdId, query, keyOf(householdId, query));
  }

  /**
   * Stops suggesting a recipe, everywhere at once.
   *
   * Optimistic: the card goes as the thumb lifts, and comes back if the write
   * fails. Every cached answer is filtered, not just the one on screen — the
   * same recipe is very often in two of them, and watching it vanish from one
   * list and stay in another is worse than not having dismissed it.
   */
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

  /** Takes a dismissal back. The undo behind the toast. */
  async restore(recipeId: string): Promise<AppError | null> {
    const result = await request(() =>
      http.DELETE('/api/v1/recipes/{recipeId}/suggestion-dismissal', {
        params: { path: { recipeId } }
      })
    );

    if (!result.ok) {
      return result.error;
    }

    // Nothing is patched back into place. A restored recipe reappears the next
    // time a question is asked, which is honest: where it lands is the ranking's
    // answer, and putting it back where it was would be inventing one.
    this.#asked.clear();

    return null;
  }

  /**
   * Stops suggesting a recipe that has been deleted.
   *
   * Each question is asked once, so without this the library would go on
   * offering a recipe that opens onto nothing until the next reload.
   */
  forget(recipeId: string): void {
    this.#without(recipeId);
  }

  /** Takes one recipe out of every answer. */
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
