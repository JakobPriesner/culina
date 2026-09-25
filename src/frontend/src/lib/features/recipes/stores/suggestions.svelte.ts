import { http, request, type AppError } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

import { toSuggestion } from '../mappers';
import type { Suggestion } from '../types';

export interface SuggestionQuery {
  /** Which meal, when the caller knows. The plan always does. */
  readonly slot?: 'breakfast' | 'lunch' | 'dinner';
  /** A ceiling on total time. Honoured exactly, never treated as a preference. */
  readonly maxMinutes?: number;
  /** What the caller already has on screen or already planned. */
  readonly exclude?: readonly string[];
  readonly limit?: number;
}

/** One question, as a key, so the same one is never asked twice. */
function keyOf(householdId: string, query: SuggestionQuery): string {
  return [
    householdId,
    query.slot ?? '',
    query.maxMinutes ?? '',
    (query.exclude ?? []).join(','),
    query.limit ?? ''
  ].join('|');
}

/** One question's answer, and how far along it is. */
interface Answer {
  readonly items: Suggestion[];
  readonly status: LoadStatus;
}

/**
 * What this person might want to cook, for one occasion.
 *
 * A bounded set with a reason for each, never a feed. Ranking the whole
 * collection is the recipe store's job with `sort: 'suggested'` — a cookbook is
 * a view of the library rather than a second one, and so is a suggestion.
 */
class SuggestionStore {
  /**
   * One entry per question.
   *
   * Status is per question rather than one flag for the store, because two
   * occasions are regularly in flight at once — the panel at the top of the
   * library and the plan's picker — and a shared flag would have each of them
   * reporting the other's progress.
   */
  #answers = $state<Record<string, Answer>>({});
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

  get error(): AppError | null {
    return this.#error;
  }

  /** The answer to one question, or nothing until it has been asked. */
  for(householdId: string | null, query: SuggestionQuery = {}): readonly Suggestion[] {
    return householdId ? (this.#answers[keyOf(householdId, query)]?.items ?? []) : [];
  }

  statusOf(householdId: string | null, query: SuggestionQuery = {}): LoadStatus {
    return householdId ? (this.#answers[keyOf(householdId, query)]?.status ?? 'idle') : 'idle';
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
    const before = this.#answers;

    this.#answers = Object.fromEntries(
      Object.entries(before).map(([key, answer]) => [
        key,
        { ...answer, items: answer.items.filter((item) => item.id !== recipeId) }
      ])
    );

    const result = await request(() =>
      http.PUT('/api/v1/recipes/{recipeId}/suggestion-dismissal', {
        params: { path: { recipeId } }
      })
    );

    if (!result.ok) {
      this.#answers = before;

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

  reset(): void {
    this.#answers = {};
    this.#error = null;
    this.#asked.clear();
  }

  async #fetch(householdId: string, query: SuggestionQuery, key: string): Promise<void> {
    this.#answers = {
      ...this.#answers,
      [key]: { items: this.#answers[key]?.items ?? [], status: 'loading' }
    };
    this.#error = null;

    const result = await request(() =>
      http.GET('/api/v1/suggestions', {
        params: {
          query: {
            householdId,
            slot: query.slot,
            maxMinutes: query.maxMinutes,
            exclude: query.exclude ? [...query.exclude] : undefined,
            limit: query.limit
          }
        }
      })
    );

    if (result.ok) {
      this.#answers = {
        ...this.#answers,
        [key]: { items: result.value.items.map(toSuggestion), status: 'ready' }
      };

      return;
    }

    this.#error = result.error;
    this.#answers = {
      ...this.#answers,
      [key]: { items: this.#answers[key]?.items ?? [], status: 'failed' }
    };
  }
}

export const suggestions = new SuggestionStore();

registerStore(() => suggestions.reset());
