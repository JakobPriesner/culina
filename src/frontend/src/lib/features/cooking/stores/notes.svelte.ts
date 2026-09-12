import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

/**
 * What one person has learned about a recipe.
 *
 * Person-owned, never household-owned: "less sugar next time" is an opinion,
 * and two people in one household can hold different ones without either of
 * them editing the recipe. That separation is the whole reason notes exist as
 * their own thing rather than as an extra field on the recipe.
 */
class NotesStore {
  #overall = $state('');
  #loaded = $state(false);

  get overall(): string {
    return this.#overall;
  }

  get loaded(): boolean {
    return this.#loaded;
  }

  async load(recipeId: string): Promise<void> {
    this.#loaded = false;

    const result = await request(() =>
      http.GET('/api/v1/recipes/{recipeId}/notes', { params: { path: { recipeId } } })
    );

    this.#overall = result.ok ? (result.value.overall ?? '') : '';
    this.#loaded = true;
  }

  /** Held locally as it is typed; the page decides when to send it. */
  set(text: string): void {
    this.#overall = text;
  }

  async save(recipeId: string): Promise<AppError | null> {
    const result = await request(() =>
      http.PUT('/api/v1/recipes/{recipeId}/notes', {
        params: { path: { recipeId } },
        // Blank means "no note" rather than an empty one: a note nobody wrote
        // should not take up space on the page.
        body: { overall: this.#overall.trim() || null, steps: [] }
      })
    );

    return result.ok ? null : result.error;
  }

  reset(): void {
    this.#overall = '';
    this.#loaded = false;
  }
}

export const notes = new NotesStore();

registerStore(() => notes.reset());
