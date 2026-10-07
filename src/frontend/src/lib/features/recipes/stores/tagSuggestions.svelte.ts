import { http, request } from '$api';
import { registerStore } from '$shell/stores';

/** A tag a recipe could carry: the household's own, with its slug, or a new word. */
export interface TagSuggestion {
  readonly name: string;
  readonly slug: string | null;
}

/** The tags offered per saved version; asked again after each save since suggestions follow what was last saved. */
class TagSuggestionStore {
  #answers = $state<Record<string, readonly TagSuggestion[]>>({});

  /** Versions already asked about; plain, not $state: `load` runs in an `$effect` and a readable guard would loop. */
  #asked = new Set<string>();

  of(recipeId: string, version: number): readonly TagSuggestion[] {
    return this.#answers[`${recipeId}@${version}`] ?? [];
  }

  /** Asks once per saved version. Nothing is shown for a failure: these are a courtesy. */
  async load(recipeId: string, version: number): Promise<void> {
    const key = `${recipeId}@${version}`;

    if (this.#asked.has(key)) {
      return;
    }

    this.#asked.add(key);

    const result = await request(() =>
      http.GET('/api/v1/recipes/{recipeId}/tag-suggestions', { params: { path: { recipeId } } })
    );

    if (result.ok) {
      this.#answers = {
        ...this.#answers,
        [key]: result.value.items.map((one) => ({ name: one.name, slug: one.slug ?? null }))
      };
    }
  }

  reset(): void {
    this.#answers = {};
    this.#asked.clear();
  }
}

export const tagSuggestions = new TagSuggestionStore();

registerStore(() => tagSuggestions.reset());
