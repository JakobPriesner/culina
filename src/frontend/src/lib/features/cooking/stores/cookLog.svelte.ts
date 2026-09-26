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

/** One time it was made, as the strip of attempts shows it. */
export type CookLogItem = CookLog['items'][number];

class CookLogStore {
  #log = $state<CookLog | null>(null);

  get count(): number {
    return this.#log?.count ?? 0;
  }

  get lastMadeAt(): string | null {
    return this.#log?.lastMadeAt ?? null;
  }

  /** Every attempt, newest first. */
  get items(): readonly CookLogItem[] {
    return this.#log?.items ?? [];
  }

  async load(recipeId: string): Promise<void> {
    const result = await request(() =>
      http.GET('/api/v1/recipes/{recipeId}/cook-log', { params: { path: { recipeId } } })
    );

    this.#log = result.ok ? result.value : null;
  }

  /**
   * One tap. The response carries the new count, so nothing has to be refetched.
   *
   * Written into the household it was cooked in, which for an inherited recipe
   * is not the one it belongs to: the history is this kitchen's.
   */
  async record(
    recipeId: string,
    servings: number,
    householdId: string | null = null
  ): Promise<Recorded | null> {
    const result = await request(() =>
      http.POST('/api/v1/recipes/{recipeId}/cook-log', {
        params: { path: { recipeId } },
        body: { servings, householdId }
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

  /**
   * Hangs a photograph on one attempt.
   *
   * The whole log comes back rather than the one entry, because the strip shows
   * all of them and a single entry would leave the client refetching the rest
   * to draw anything.
   */
  async setPhoto(recipeId: string, entryId: string, file: File): Promise<boolean> {
    const body = new FormData();

    body.append('file', file);

    const result = await request(() =>
      http.PUT('/api/v1/recipes/{recipeId}/cook-log/{entryId}/photo', {
        params: { path: { recipeId, entryId } },
        body: body as unknown as { file: string },
        // FormData sets its own multipart boundary; serialising it as JSON
        // would send the string "[object FormData]".
        bodySerializer: (value: unknown) => value as FormData
      })
    );

    if (result.ok) {
      this.#log = result.value;
    }

    return result.ok;
  }

  async removePhoto(recipeId: string, entryId: string): Promise<boolean> {
    const result = await request(() =>
      http.DELETE('/api/v1/recipes/{recipeId}/cook-log/{entryId}/photo', {
        params: { path: { recipeId, entryId } }
      })
    );

    if (result.ok) {
      this.#log = result.value;
    }

    return result.ok;
  }

  reset(): void {
    this.#log = null;
  }
}

export const cookLog = new CookLogStore();

registerStore(() => cookLog.reset());
