import { http, request } from '$api';
import { registerStore } from '$shell/stores';

import type { components } from '$api/generated/schema';

/**
 * How often this person has made a recipe; personal like notes, and why Culina has no star ratings.
 */
type CookLog = components['schemas']['RecipesGetCookLogResponse'];

export interface Recorded {
  readonly entryId: string;
  readonly count: number;
}

export type CookLogItem = CookLog['items'][number];

class CookLogStore {
  #log = $state<CookLog | null>(null);

  get count(): number {
    return this.#log?.count ?? 0;
  }

  get lastMadeAt(): string | null {
    return this.#log?.lastMadeAt ?? null;
  }

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
   * One tap; the response carries the new count. Written into the household it was cooked in, which
   * for an inherited recipe is this kitchen's.
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

  async undo(recipeId: string, entryId: string): Promise<void> {
    await request(() =>
      http.DELETE('/api/v1/recipes/{recipeId}/cook-log/{entryId}', {
        params: { path: { recipeId, entryId } }
      })
    );

    await this.load(recipeId);
  }

  /**
   * Hangs a photograph on one attempt; the whole log comes back since the strip shows every entry.
   */
  async setPhoto(recipeId: string, entryId: string, file: File): Promise<boolean> {
    const body = new FormData();

    body.append('file', file);

    const result = await request(() =>
      http.PUT('/api/v1/recipes/{recipeId}/cook-log/{entryId}/photo', {
        params: { path: { recipeId, entryId } },
        body: body as unknown as { file: string },
        // FormData sets its own multipart boundary; JSON-serialising it would send "[object
        // FormData]".
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
