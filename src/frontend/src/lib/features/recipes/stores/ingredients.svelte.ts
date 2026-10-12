import { http, request } from '$api';
import { registerStore } from '$shell/stores';

export interface IngredientSuggestion {
  readonly name: string;
  readonly section: string;
  readonly own: boolean;
  /** The household's own name for what was typed, spelt otherwise; at most one, and first. */
  readonly equivalent: boolean;
}

/** How a typed name reads, in the words it was typed in. */
export interface TypedName {
  /** The thing and its kinds, what lines of it are merged and looked up as: "Kokosmilch, light". */
  readonly understood: string;
  /** The name without what is done to it in the kitchen: "Zwiebeln" for "gehackte Zwiebeln". */
  readonly name: string | null;
  /** What is done to it, which belongs in the note: "gehackt". */
  readonly note: string | null;
}

/**
 * What an ingredient line could be about: the household's own words first, then a seeded list;
 * suggestions only, never required.
 */
class IngredientStore {
  #items = $state<IngredientSuggestion[]>([]);
  #typed = $state<TypedName | null>(null);
  /** The query `items` answer, so a field can tell its own answer from another field's or an older one. */
  #answered = $state('');
  /** The query the current items answer, so a stale reply can't overwrite. */
  #asked = '';

  get items(): readonly IngredientSuggestion[] {
    return this.#items;
  }

  /** How the answered query reads. */
  get typed(): TypedName | null {
    return this.#typed;
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
    this.#typed = result.ok ? (result.value.typed ?? null) : null;
    this.#answered = query;
  }

  clear(): void {
    this.#asked = '';
    this.#answered = '';
    this.#items = [];
    this.#typed = null;
  }

  reset(): void {
    this.clear();
  }
}

export const ingredients = new IngredientStore();

registerStore(() => ingredients.reset());
