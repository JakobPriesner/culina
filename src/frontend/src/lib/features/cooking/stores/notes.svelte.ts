import { http, request, type AppError } from '$api';
import { LatestRead, registerStore } from '$shell/stores';

/** One person's notes on a recipe; person-owned, so two household members can differ without editing the recipe. */
class NotesStore {
  #overall = $state('');
  #loaded = $state(false);
  /** The read failed; kept apart from "no note", or saving would overwrite a note never shown. */
  #failed = $state(false);
  /** Notes on single steps, shown under the step; a save replaces the whole note, so every one goes back. */
  #steps = $state<readonly { stepId: string; body: string }[]>([]);
  /** The save in flight; not `$state` because `load()` reads it before its first await, inside an effect. */
  #saving: Promise<unknown> | null = null;
  /** The recipe the note on screen belongs to, and which read is the latest; plain for the same reason as `#saving`. */
  #recipeId: string | null = null;
  #reads = new LatestRead();

  get overall(): string {
    return this.#overall;
  }

  get loaded(): boolean {
    return this.#loaded;
  }

  get failed(): boolean {
    return this.#failed;
  }

  /** Whether there is anything written, overall or on a step: what cook mode marks its notes button for. */
  get written(): boolean {
    return this.#overall.trim() !== '' || this.#steps.some((note) => note.body.trim() !== '');
  }

  stepNote(stepId: string): string {
    return this.#steps.find((note) => note.stepId === stepId)?.body ?? '';
  }

  async load(recipeId: string): Promise<void> {
    const isLatest = this.#reads.start();

    this.#recipeId = recipeId;
    // Emptied before the read: until it arrives the screen holds the previous recipe's note.
    this.#overall = '';
    this.#steps = [];
    this.#loaded = false;
    this.#failed = false;

    // A write sent as the page closes for cook mode must land before this read, or the note shows stale.
    await this.#saving;

    const result = await request(() =>
      http.GET('/api/v1/recipes/{recipeId}/notes', { params: { path: { recipeId } } })
    );

    // Another recipe (or a later read of this one) has been asked for since; its answer is the one to show.
    if (!isLatest()) {
      return;
    }

    if (!result.ok) {
      this.#failed = true;
      return;
    }

    this.#overall = result.value.overall ?? '';
    this.#steps = result.value.steps;
    this.#loaded = true;
  }

  set(text: string): void {
    this.#overall = text;
  }

  setStep(stepId: string, text: string): void {
    const others = this.#steps.filter((note) => note.stepId !== stepId);

    this.#steps = text ? [...others, { stepId, body: text }] : others;
  }

  async save(recipeId: string): Promise<AppError | null> {
    // A note that was never read would be saved over the one the server holds, and typing is never sent to another recipe.
    if (!this.#loaded || recipeId !== this.#recipeId) {
      return null;
    }

    // Taken now, not at request build (a retry builds again after a next-recipe read may have emptied it).
    // Blank means no note.
    const body = {
      overall: this.#overall.trim() || null,
      steps: this.#steps
        .map((note) => ({ stepId: note.stepId, body: note.body.trim() }))
        .filter((note) => note.body !== '')
    };

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
    this.#recipeId = null;
    this.#reads.cancel();
  }
}

export const notes = new NotesStore();

registerStore(() => notes.reset());
