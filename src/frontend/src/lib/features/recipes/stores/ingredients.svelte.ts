import { http, request } from '$api';
import { registerStore } from '$shell/stores';

export interface IngredientSuggestion {
  readonly name: string;
  readonly section: string;
  readonly own: boolean;
}

/**
 * What an ingredient line could be about: the household's own words first, then a seeded list;
 * suggestions only, never required.
 */
class IngredientStore {
  #items = $state<IngredientSuggestion[]>([]);
  /** The query the current items answer, so a stale reply can't overwrite. */
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

    // Replies arrive out of order (a short query answers last): only the reply to the current
    // question may write.
    if (this.#asked !== asked) {
      return;
    }

    // Suggestions are a convenience; a message about having none would be worse.
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
