import { ask as askServer, clientError, type AppError, type Stream } from '$api';
import { registerStore } from '$shell/stores';

import type { Draft } from '../draftToRecipe';

/** Which of the three that post JSON. A photograph posts multipart and is its own method. */
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

/** Reading one out of a photograph, which goes up as multipart. */
interface Read {
  householdId: string;
  language: string;
  file: File;
}

/** One event as the stream sends it. */
interface DraftEventWire {
  draft: Draft;
  finished: boolean;
  problem?: { code: string; detail: string } | null;
}

/**
 * Asking the assistant for a recipe, and watching it be written.
 *
 * A stream rather than a request that eventually answers. A model takes tens of
 * seconds over a recipe, and the difference between a spinner for forty seconds
 * and a title appearing at three, ingredients at eight and steps filling in
 * after that is the difference between waiting and reading.
 *
 * So `draft` is not the answer: it is the answer so far, and it changes several
 * times per ask. `asking` says whether more is coming. A caller that wants the
 * finished article awaits the promise, which is what every existing one does.
 *
 * All four kinds live here, including the photograph — it posts multipart where
 * the others post JSON, and that was the only reason it used to be somewhere
 * else. Everything either side of the request is identical, and one copy of
 * "what to do when a draft arrives in pieces" is enough.
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

  /**
   * The stream being read, if one is.
   *
   * Deliberately not `$state`: nothing renders it, and what does render is the
   * draft it is filling in.
   */
  #stream: Stream | null = null;

  /** What the assistant has written so far, or null before anything has. */
  get draft(): Draft | null {
    return this.#draft;
  }

  /** Whether more of it is still coming. */
  get asking(): boolean {
    return this.#asking;
  }

  get error(): AppError | null {
    return this.#error;
  }

  /** Asks for one, unless one is already being asked for. */
  ask(request: Ask): Promise<AppError | null> {
    return this.#follow(
      '/api/v1/recipe-drafts',
      JSON.stringify({
        kind: request.kind,
        householdId: request.householdId,
        material: request.material,
        recipeId: request.recipeId,
        language: request.language
      })
    );
  }

  /** Reads one out of a photograph, unless something is already being asked for. */
  read(request: Read): Promise<AppError | null> {
    const body = new FormData();

    body.append('file', request.file);

    const query = new URLSearchParams({
      householdId: request.householdId,
      language: request.language
    });

    return this.#follow(`/api/v1/recipe-drafts/photographs?${query}`, body);
  }

  /** Throws the draft away, accepted or not. */
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
      // A stream ends once. Both a finished event and a connection that died
      // lead here, and on a fast failure they can lead here together.
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
          // Kept even when the event that carries it also carries a failure:
          // a provider that stopped after the ingredients wrote something that
          // was paid for, and the screen can show it beside the reason.
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
