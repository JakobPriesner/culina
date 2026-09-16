import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

/**
 * The words this kitchen uses.
 *
 * There is no tag management anywhere in the app: a tag exists because a recipe
 * carries it and stops existing when the last one lets it go. So this store
 * answers one question — which ones are in use, and how much — which is what
 * the rule editor offers instead of asking somebody to guess a slug.
 */
export interface TagInUse {
  readonly slug: string;
  readonly name: string;
  readonly recipeCount: number;
}

class TagStore {
  #items = $state<TagInUse[]>([]);
  #error = $state<AppError | null>(null);

  /**
   * Which household was read, and when.
   *
   * Deliberately not `$state`. It is read inside `load`, which is called from
   * an `$effect`, and a reactive read there would make the effect depend on
   * something the same call writes.
   */
  #loadedFor: string | null = null;

  #loadedAt = 0;

  get items(): readonly TagInUse[] {
    return this.#items;
  }

  get error(): AppError | null {
    return this.#error;
  }

  /**
   * Reads the household's tags.
   *
   * Cached briefly rather than for the session: a tag exists because a recipe
   * carries it, so the vocabulary changes every time somebody saves one — and a
   * rule editor offering yesterday's words would be missing exactly the tag
   * that prompted somebody to make the shelf.
   */
  async load(householdId: string, maxAgeMs = 30_000): Promise<void> {
    if (this.#loadedFor === householdId && Date.now() - this.#loadedAt < maxAgeMs) {
      return;
    }

    this.#loadedFor = householdId;
    this.#loadedAt = Date.now();

    const result = await request(() =>
      http.GET('/api/v1/tags', { params: { query: { householdId } } })
    );

    if (result.ok) {
      this.#items = [...result.value.items];
      this.#error = null;

      return;
    }

    // Asked again next time: a vocabulary that failed to load once should not
    // stay missing for the rest of the session.
    this.#loadedFor = null;
    this.#loadedAt = 0;
    this.#error = result.error;
  }

  reset(): void {
    this.#items = [];
    this.#error = null;
    this.#loadedFor = null;
    this.#loadedAt = 0;
  }
}

export const tags = new TagStore();

registerStore(() => tags.reset());
