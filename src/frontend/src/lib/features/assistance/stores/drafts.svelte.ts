import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

import type { Draft } from '../draftToRecipe';

/** Which of the three ways a draft is asked for. */
export type DraftKind = 'idea' | 'text' | 'revision';

interface Ask {
  kind: DraftKind;
  householdId: string;
  /** The idea, or the pasted text. Not sent for a revision. */
  material?: string;
  /** Which recipe to rewrite, for a revision. */
  recipeId?: string;
  language: string;
}

/**
 * Asking the assistant for a recipe.
 *
 * One request at a time, and no queue: a second ask while one is running is
 * somebody pressing the button twice, and the honest response is to ignore it
 * rather than to spend twice.
 *
 * Nothing here is cached. A draft is read once, accepted or thrown away, and
 * keeping the last one would only make it possible to accept it into a
 * different recipe by mistake.
 */
class DraftStore {
  #draft = $state<Draft | null>(null);
  #asking = $state(false);
  #error = $state<AppError | null>(null);

  get draft(): Draft | null {
    return this.#draft;
  }

  get asking(): boolean {
    return this.#asking;
  }

  get error(): AppError | null {
    return this.#error;
  }

  /** Asks for one, unless one is already being asked for. */
  async ask(ask: Ask): Promise<AppError | null> {
    if (this.#asking) {
      return null;
    }

    this.#asking = true;
    this.#error = null;
    this.#draft = null;

    const outcome = await request(() =>
      http.POST('/api/v1/recipe-drafts', {
        body: {
          kind: ask.kind,
          householdId: ask.householdId,
          material: ask.material,
          recipeId: ask.recipeId,
          language: ask.language
        }
      })
    );

    this.#asking = false;

    if (outcome.ok) {
      this.#draft = outcome.value;

      return null;
    }

    this.#error = outcome.error;

    return outcome.error;
  }

  /** Throws the draft away, accepted or not. */
  dismiss(): void {
    this.#draft = null;
    this.#error = null;
  }

  reset(): void {
    this.#draft = null;
    this.#asking = false;
    this.#error = null;
  }
}

export const createDraftStore = (): DraftStore => new DraftStore();
export const drafts = createDraftStore();

registerStore(() => drafts.reset());
