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
  /**
   * The read did not come back. Kept apart from "no note", because an empty
   * note can be typed into and the save would replace whatever the server
   * still holds — a note the person was never shown.
   */
  #failed = $state(false);
  /**
   * The step notes the read returned. Nothing here shows or edits them, but a
   * save replaces the whole of a note, so they go back exactly as they came.
   */
  #steps: { stepId: string; body: string }[] = [];
  /**
   * The save still on its way. Plain rather than `$state`, because `load()`
   * reads it before its first await, from inside an effect.
   */
  #saving: Promise<unknown> | null = null;

  get overall(): string {
    return this.#overall;
  }

  get loaded(): boolean {
    return this.#loaded;
  }

  get failed(): boolean {
    return this.#failed;
  }

  async load(recipeId: string): Promise<void> {
    // Emptied before the read, not after: the note on screen until it arrives
    // is the previous recipe's, and must not be saved as this one's.
    this.#overall = '';
    this.#steps = [];
    this.#loaded = false;
    this.#failed = false;

    // Leaving the recipe for cook mode sends what was typed last as the page
    // closes, and cook mode reads the note straight back. A read that overtook
    // that write would show the note as it was before.
    await this.#saving;

    const result = await request(() =>
      http.GET('/api/v1/recipes/{recipeId}/notes', { params: { path: { recipeId } } })
    );

    if (!result.ok) {
      this.#failed = true;
      return;
    }

    this.#overall = result.value.overall ?? '';
    this.#steps = result.value.steps;
    this.#loaded = true;
  }

  /** Held locally as it is typed; the page decides when to send it. */
  set(text: string): void {
    this.#overall = text;
  }

  async save(recipeId: string): Promise<AppError | null> {
    // A note that was never read would be saved over the one the server holds.
    if (!this.#loaded) {
      return null;
    }

    // Taken now, not when the request is built: a retry builds it again, after
    // the next recipe's read may have emptied the note.
    //
    // Blank means "no note" rather than an empty one: a note nobody wrote
    // should not take up space on the page.
    const body = { overall: this.#overall.trim() || null, steps: this.#steps };

    const saving = request(() =>
      http.PUT('/api/v1/recipes/{recipeId}/notes', { params: { path: { recipeId } }, body })
    );

    this.#saving = saving;

    const result = await saving;

    if (this.#saving === saving) {
      this.#saving = null;
    }

    return result.ok ? null : result.error;
  }

  reset(): void {
    this.#overall = '';
    this.#loaded = false;
    this.#failed = false;
    this.#steps = [];
    this.#saving = null;
  }
}

export const notes = new NotesStore();

registerStore(() => notes.reset());
