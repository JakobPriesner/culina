import { http, request } from '$api';
import { registerStore } from '$shell/stores';

/** One thing a recipe could call for. */
export interface IngredientSuggestion {
  readonly name: string;
  /** Where in a shop it is found. */
  readonly section: string;
  /** Whether this household has written it before. */
  readonly own: boolean;
}

/**
 * What an ingredient line could be about.
 *
 * The household's own words first — after a few recipes they are how these
 * particular people talk about food — then a short seeded list of what a home
 * kitchen buys, so an empty kitchen is not offered nothing.
 *
 * Suggestions only. An ingredient is whatever somebody types, and nothing here
 * ever has to be chosen.
 */
class IngredientStore {
  #items = $state<IngredientSuggestion[]>([]);
  /** The query the current items answer, so a stale reply cannot overwrite. */
  #asked = '';

  get items(): readonly IngredientSuggestion[] {
    return this.#items;
  }

  async suggest(householdId: string, query: string, language: string): Promise<void> {
    if (!householdId) {
      return;
    }

    const asked = `${householdId}|${query}|${language}`;

    this.#asked = asked;

    const result = await request(() =>
      http.GET('/api/v1/households/{householdId}/ingredients', {
        params: { path: { householdId }, query: { q: query, language } }
      })
    );

    // Typing fast sends several of these and they do not come back in order: a
    // short query matches more rows and regularly answers last. Only the reply
    // to the question still being asked is allowed to write.
    if (this.#asked !== asked) {
      return;
    }

    // Nothing is said about this failing. Suggestions are a convenience, and a
    // message about not having any would be worse than not having any.
    this.#items = result.ok ? [...result.value.items] : [];
  }

  clear(): void {
    this.#asked = '';
    this.#items = [];
  }

  reset(): void {
    this.clear();
  }
}

export const ingredients = new IngredientStore();

registerStore(() => ingredients.reset());
