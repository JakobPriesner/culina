import { http, request } from '$api';
import { registerStore } from '$shell/stores';

import type { components } from '$api/generated/schema';

/**
 * How often this person has made a recipe.
 *
 * Personal, like notes: two people in one household keep separate histories,
 * because what *you* cooked is the useful fact. It is also why Culina has no
 * star ratings — what someone actually cooked is a better signal than what they
 * once claimed to like.
 */
type CookLog = components['schemas']['RecipesGetCookLogResponse'];

export interface Recorded {
  readonly entryId: string;
  readonly count: number;
}

class CookLogStore {
  #log = $state<CookLog | null>(null);

  get count(): number {
    return this.#log?.count ?? 0;
  }

  get lastMadeAt(): string | null {
    return this.#log?.lastMadeAt ?? null;
  }

  async load(recipeId: string): Promise<void> {
    const result = await request(() =>
      http.GET('/api/v1/recipes/{recipeId}/cook-log', { params: { path: { recipeId } } })
    );

    this.#log = result.ok ? result.value : null;
  }

  /** One tap. The response carries the new count, so nothing has to be refetched. */
  async record(recipeId: string, servings: number): Promise<Recorded | null> {
    const result = await request(() =>
      http.POST('/api/v1/recipes/{recipeId}/cook-log', {
        params: { path: { recipeId } },
        body: { servings }
      })
    );

    if (!result.ok) {
      return null;
    }

    await this.load(recipeId);

    return { entryId: result.value.entryId, count: result.value.count };
  }

  /** The undo behind the toast, which is why there is no "are you sure?". */
  async undo(recipeId: string, entryId: string): Promise<void> {
    await request(() =>
      http.DELETE('/api/v1/recipes/{recipeId}/cook-log/{entryId}', {
        params: { path: { recipeId, entryId } }
      })
    );

    await this.load(recipeId);
  }

  reset(): void {
    this.#log = null;
  }
}

export const cookLog = new CookLogStore();

registerStore(() => cookLog.reset());
