import { ask as askServer, clientError, type AppError, type Stream } from '$api';
import { registerStore } from '$shell/stores';

import type { Draft } from '../draftToRecipe';

/** The kinds that post JSON; a photograph posts multipart. */
export type DraftKind = 'idea' | 'text' | 'revision' | 'social';

interface Ask {
  kind: DraftKind;
  householdId: string;
  material?: string;
  recipeId?: string;
  language: string;
  transcript?: string;
}

interface Read {
  householdId: string;
  language: string;
  file: File;
}

interface DraftEventWire {
  draft: Draft;
  finished: boolean;
  problem?: { code: string; detail: string } | null;
}

/**
 * Asks the assistant for a recipe and streams it: `draft` is the answer so far, `asking` says whether more is coming.
 * One request at a time, no queue, no cache: a second ask is a double press, and a kept draft could be accepted into the wrong recipe.
 */
class DraftStore {
  #draft = $state<Draft | null>(null);
  #asking = $state(false);
  #error = $state<AppError | null>(null);

  /** The stream being read; not `$state`, since only the draft it fills is rendered. */
  #stream: Stream | null = null;

  get draft(): Draft | null {
    return this.#draft;
  }

  get asking(): boolean {
    return this.#asking;
  }

  get error(): AppError | null {
    return this.#error;
  }

  ask(request: Ask): Promise<AppError | null> {
    return this.#follow(
      '/api/v1/recipe-drafts',
      JSON.stringify({
        kind: request.kind,
        householdId: request.householdId,
        material: request.material,
        transcript: request.transcript,
        recipeId: request.recipeId,
        language: request.language
      })
    );
  }

  read(request: Read): Promise<AppError | null> {
    const body = new FormData();

    body.append('file', request.file);

    const query = new URLSearchParams({
      householdId: request.householdId,
      language: request.language
    });

    return this.#follow(`/api/v1/recipe-drafts/photographs?${query}`, body);
  }

  /** All screenshots and their caption form one read job and one budget reservation. */
  readMedia(request: {
    householdId: string;
    language: string;
    photos: readonly File[];
    material: string;
    transcript: string;
  }): Promise<AppError | null> {
    const body = new FormData();
    for (const file of request.photos) body.append('photos', file);
    body.append('material', request.material);
    body.append('transcript', request.transcript);
    const query = new URLSearchParams({
      householdId: request.householdId,
      language: request.language
    });
    return this.#follow(`/api/v1/recipe-drafts/media?${query}`, body);
  }

  dismiss(): void {
    this.#stop();
    this.#draft = null;
    this.#error = null;
  }

  reset(): void {
    this.dismiss();
  }

  #follow(path: string, body: BodyInit): Promise<AppError | null> {
    if (this.#asking) {
      return Promise.resolve(null);
    }

    this.#asking = true;
    this.#error = null;
    this.#draft = null;

    return new Promise((resolve) => {
      // A stream ends once; a finished event and a dead connection can both arrive.
      let settled = false;

      const settle = (error: AppError | null) => {
        if (settled) {
          return;
        }

        settled = true;
        this.#stop();
        this.#error = error;
        resolve(error);
      };

      this.#stream = askServer<DraftEventWire>(path, body, {
        message: (event) => {
          // Kept even alongside a failure: a stopped provider's partial draft was paid for.
          this.#draft = event.draft;

          if (event.finished) {
            settle(event.problem ? clientError(event.problem.code, event.problem.detail) : null);
          }
        },
        failed: settle
      });
    });
  }

  #stop(): void {
    this.#stream?.close();
    this.#stream = null;
    this.#asking = false;
  }
}

export const createDraftStore = (): DraftStore => new DraftStore();
export const drafts = createDraftStore();

registerStore(() => drafts.reset());
