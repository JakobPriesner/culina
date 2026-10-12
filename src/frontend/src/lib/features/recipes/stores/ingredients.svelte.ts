import { http, request } from '$api';
import { registerStore } from '$shell/stores';

export interface IngredientSuggestion {
  readonly name: string;
  readonly section: string;
  readonly own: boolean;
  /** The household's own name for what was typed, spelt otherwise; at most one, and first. */
  readonly equivalent: boolean;
}

/**
 * What an ingredient line could be about: the household's own words first, then a seeded list;
 * suggestions only, never required.
 */
class IngredientStore {
  #items = $state<IngredientSuggestion[]>([]);
  /** The query `items` answer, so a field can tell its own answer from another field's or an older one. */
  #answered = $state('');
  /** The query the current items answer, so a stale reply can't overwrite. */
  #asked = '';

  get items(): readonly IngredientSuggestion[] {
    return this.#items;
  }

  get answered(): string {
    return this.#answered;
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
    this.#answered = query;
  }

  clear(): void {
    this.#asked = '';
    this.#answered = '';
    this.#items = [];
  }

  reset(): void {
    this.clear();
  }
}

export const ingredients = new IngredientStore();

registerStore(() => ingredients.reset());
