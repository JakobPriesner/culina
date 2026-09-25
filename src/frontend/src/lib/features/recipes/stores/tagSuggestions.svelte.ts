import { http, request } from '$api';
import { registerStore } from '$shell/stores';

/** A tag a recipe could carry: the household's own, with its slug, or a new word. */
export interface TagSuggestion {
  readonly name: string;
  readonly slug: string | null;
}

/**
 * The tags each recipe is offered, per saved version.
 *
 * Asked again whenever the recipe has been saved, because what it is read as
 * is what was last saved — a new title is a new set of suggestions once it
 * has reached the server, and not before.
 */
class TagSuggestionStore {
  #answers = $state<Record<string, readonly TagSuggestion[]>>({});

  /**
   * Which versions have been asked about.
   *
   * A plain field, not `$state`: `load` is called from an `$effect`, and a
   * guard that effect could read would make it depend on what `load` writes,
   * and ask forever.
   */
  #asked = new Set<string>();

  /** The suggestions for this version of the recipe, or none until they are in. */
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
