import { http, request, type AppError } from '$api';
import { LatestRead, registerStore, type LoadStatus } from '$shell/stores';

import { toFact } from '../mappers';
import type { IngredientFact } from '../types';

/**
 * What one household said about its ingredients, for the settings page: the switch for typical weights, and
 * the food choices and weights by name. A change shows at once; when the server refuses, the list goes back
 * to what it was. `load` only writes, so the `$effect` that calls it cannot ask forever.
 */
class IngredientFactsStore {
  #householdId = $state<string | null>(null);
  #facts = $state<readonly IngredientFact[]>([]);
  #useTypicalWeights = $state(true);
  #status = $state<LoadStatus>('idle');
  #latest = new LatestRead();

  facts(householdId: string): readonly IngredientFact[] {
    return this.#householdId === householdId ? this.#facts : [];
  }

  useTypicalWeights(householdId: string): boolean {
    return this.#householdId !== householdId || this.#useTypicalWeights;
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
    const path = { householdId };

    const [facts, settings] = await Promise.all([
      request(() =>
        http.GET('/api/v1/households/{householdId}/nutrition/ingredients', { params: { path } })
      ),
      request(() => http.GET('/api/v1/households/{householdId}/nutrition', { params: { path } }))
    ]);

    if (!current()) {
      return;
    }

    if (!facts.ok || !settings.ok) {
      this.#status = 'failed';

      return;
    }

    this.#facts = facts.value.items.map(toFact);
    this.#useTypicalWeights = settings.value.useTypicalWeights;
    this.#status = 'ready';
  }

  /** The switch moves at once; a refusal moves it back. */
  async setUseTypicalWeights(householdId: string, on: boolean): Promise<AppError | null> {
    const before = this.#useTypicalWeights;

    this.#useTypicalWeights = on;

    const result = await request(() =>
      http.PUT('/api/v1/households/{householdId}/nutrition', {
        params: { path: { householdId } },
        body: { useTypicalWeights: on }
      })
    );

    if (!result.ok) {
      this.#useTypicalWeights = before;

      return result.error;
    }

    return null;
  }

  /** Forgets one weight; the name leaves the list with its last fact. */
  removeWeight(householdId: string, name: string, unit: string): Promise<AppError | null> {
    return this.#change(
      householdId,
      (fact) =>
        fact.name === name
          ? { ...fact, weights: fact.weights.filter((weight) => weight.unit !== unit) }
          : fact,
      () =>
        http.DELETE('/api/v1/households/{householdId}/ingredients/{name}/units/{unit}', {
          params: { path: { householdId, name, unit } }
        })
    );
  }

  setWeight(
    householdId: string,
    name: string,
    unit: string,
    grams: number
  ): Promise<AppError | null> {
    return this.#change(
      householdId,
      (fact) =>
        fact.name === name
          ? {
              ...fact,
              weights: fact.weights.map((weight) =>
                weight.unit === unit ? { ...weight, grams } : weight
              )
            }
          : fact,
      () =>
        http.PUT('/api/v1/households/{householdId}/ingredients/{name}/units/{unit}', {
          params: { path: { householdId, name, unit } },
          body: { grams }
        })
    );
  }

  /** A null code is "do not count this". */
  setFood(householdId: string, name: string, code: string | null): Promise<AppError | null> {
    return this.#change(
      householdId,
      (fact) => fact,
      () =>
        http.PUT('/api/v1/households/{householdId}/ingredients/{name}', {
          params: { path: { householdId, name } },
          body: { food: code }
        })
    );
  }

  removeFood(householdId: string, name: string): Promise<AppError | null> {
    return this.#change(
      householdId,
      (fact) => (fact.name === name ? { ...fact, corrected: false, food: null } : fact),
      () =>
        http.DELETE('/api/v1/households/{householdId}/ingredients/{name}', {
          params: { path: { householdId, name } }
        })
    );
  }

  /** Shows the change, tells the server, and on success reads the list again: only it knows what a name folds to and what a food is called. */
  async #change(
    householdId: string,
    change: (fact: IngredientFact) => IngredientFact,
    send: Parameters<typeof request<unknown>>[0]
  ): Promise<AppError | null> {
    const before = this.#facts;
    const mine = this.#householdId === householdId;

    if (mine) {
      this.#facts = before.map(change).filter((fact) => fact.corrected || fact.weights.length > 0);
    }

    const result = await request(send);

    if (!result.ok) {
      if (mine && this.#householdId === householdId) {
        this.#facts = before;
      }

      return result.error;
    }

    if (this.#householdId === householdId) {
      await this.#read(householdId);
    }

    return null;
  }

  reset(): void {
    this.#latest.cancel();
    this.#householdId = null;
    this.#facts = [];
    this.#useTypicalWeights = true;
    this.#status = 'idle';
  }
}

export const ingredientFacts = new IngredientFactsStore();

registerStore(() => ingredientFacts.reset());
