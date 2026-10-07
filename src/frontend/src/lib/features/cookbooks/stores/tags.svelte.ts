import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

/** The tags in use and how much. There is no tag management (a tag exists while a recipe carries it); the rule editor offers these instead of a guessed slug. */
export interface TagInUse {
  readonly slug: string;
  readonly name: string;
  readonly recipeCount: number;
}

class TagStore {
  #items = $state<TagInUse[]>([]);
  #error = $state<AppError | null>(null);

  /** Household and time of the last read; not `$state`, since `load` runs in an `$effect` and a reactive read would loop. */
  #loadedFor: string | null = null;

  #loadedAt = 0;

  get items(): readonly TagInUse[] {
    return this.#items;
  }

  get error(): AppError | null {
    return this.#error;
  }

  /** Reads the household's tags, cached briefly: the vocabulary changes whenever a recipe is saved. */
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

    // Retry next time instead of staying missing for the session.
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
