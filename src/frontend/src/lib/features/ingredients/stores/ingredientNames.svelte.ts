import { http, ok, request, type AppError, type Result } from '$api';
import { LatestRead, registerStore, type LoadStatus } from '$shell/stores';

import { toMerge, toName, toSuggestion } from '../mappers';
import type { IngredientMerge, IngredientName, MergeSuggestion } from '../types';

/**
 * One household's ingredient names for the settings page: the names its recipes use, the merges it made, and the
 * merges the server suggests. A merge or a dismissal shows at once; a refusal puts the lists back. After a merge the
 * lists are read again, since only the server knows which spellings folded together and how many recipes changed.
 * `load` only writes, so the `$effect` that calls it cannot ask forever.
 */
class IngredientNamesStore {
  #householdId = $state<string | null>(null);
  #names = $state<readonly IngredientName[]>([]);
  #merges = $state<readonly IngredientMerge[]>([]);
  #suggestions = $state<readonly MergeSuggestion[]>([]);
  #status = $state<LoadStatus>('idle');
  #latest = new LatestRead();

  names(householdId: string): readonly IngredientName[] {
    return this.#householdId === householdId ? this.#names : [];
  }

  merges(householdId: string): readonly IngredientMerge[] {
    return this.#householdId === householdId ? this.#merges : [];
  }

  suggestions(householdId: string): readonly MergeSuggestion[] {
    return this.#householdId === householdId ? this.#suggestions : [];
  }

  status(householdId: string): LoadStatus {
    return this.#householdId === householdId ? this.#status : 'idle';
  }

  load(householdId: string): Promise<void> {
    this.#householdId = householdId;
    this.#status = 'loading';

    return this.#read(householdId);
  }

  async #read(householdId: string): Promise<void> {
    const current = this.#latest.start();
    const params = { path: { householdId } };

    const [names, merges, suggestions] = await Promise.all([
      request(() => http.GET('/api/v1/households/{householdId}/ingredient-names', { params })),
      request(() => http.GET('/api/v1/households/{householdId}/ingredient-merges', { params })),
      request(() =>
        http.GET('/api/v1/households/{householdId}/ingredient-merge-suggestions', { params })
      )
    ]);

    if (!current()) {
      return;
    }

    if (!names.ok || !merges.ok || !suggestions.ok) {
      this.#status = 'failed';

      return;
    }

    this.#names = names.value.items.map(toName);
    this.#merges = merges.value.items.map(toMerge);
    this.#suggestions = suggestions.value.items.map(toSuggestion);
    this.#status = 'ready';
  }

  /** Merges `from` into `to`, which renames it when `to` is not in use yet. Answers the name actually kept. */
  async merge(householdId: string, from: string, to: string): Promise<Result<string>> {
    const result = await this.#change(
      householdId,
      () => {
        this.#suggestions = this.#suggestions.filter((one) => one.from.name !== from);
        this.#names = this.#names.filter((one) => one.name !== from);
      },
      () =>
        http.POST('/api/v1/households/{householdId}/ingredient-merges', {
          params: { path: { householdId } },
          body: { from, to }
        })
    );

    return result.ok ? ok(result.value.to) : result;
  }

  /** Accepts every suggestion, one after the other; stops at the first refusal. */
  async mergeAll(householdId: string): Promise<{ merged: number; error: AppError | null }> {
    let merged = 0;

    for (const suggestion of [...this.suggestions(householdId)]) {
      const result = await this.merge(householdId, suggestion.from.name, suggestion.to.name);

      if (!result.ok) {
        return { merged, error: result.error };
      }

      merged++;
    }

    return { merged, error: null };
  }

  /** Stops applying a merge; recipes already rewritten keep the kept name. */
  async removeMerge(householdId: string, from: string): Promise<AppError | null> {
    const result = await this.#change(
      householdId,
      () => {
        this.#merges = this.#merges.filter((one) => one.from !== from);
      },
      () =>
        http.DELETE('/api/v1/households/{householdId}/ingredient-merges/{from}', {
          params: { path: { householdId, from } }
        })
    );

    return result.ok ? null : result.error;
  }

  /** Says `from` is its own ingredient: never suggested again. */
  async keepApart(householdId: string, from: string): Promise<AppError | null> {
    const result = await this.#change(
      householdId,
      () => {
        this.#suggestions = this.#suggestions.filter((one) => one.from.name !== from);
      },
      () =>
        http.DELETE('/api/v1/households/{householdId}/ingredient-merge-suggestions/{name}', {
          params: { path: { householdId, name: from } }
        })
    );

    return result.ok ? null : result.error;
  }

  /** Shows the change, tells the server, and reads everything again; a refusal restores what was shown. */
  async #change<T>(
    householdId: string,
    show: () => void,
    send: Parameters<typeof request<T>>[0]
  ): Promise<Result<T>> {
    const mine = this.#householdId === householdId;
    const before = { names: this.#names, merges: this.#merges, suggestions: this.#suggestions };

    if (mine) {
      show();
    }

    const result = await request(send);

    if (!result.ok) {
      if (mine && this.#householdId === householdId) {
        this.#names = before.names;
        this.#merges = before.merges;
        this.#suggestions = before.suggestions;
      }

      return result;
    }

    if (this.#householdId === householdId) {
      await this.#read(householdId);
    }

    return result;
  }

  reset(): void {
    this.#latest.cancel();
    this.#householdId = null;
    this.#names = [];
    this.#merges = [];
    this.#suggestions = [];
    this.#status = 'idle';
  }
}

export const ingredientNames = new IngredientNamesStore();

registerStore(() => ingredientNames.reset());
