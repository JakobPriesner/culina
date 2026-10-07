import { http, request, watch, type AppError, type Stream } from '$api';

import type { ImportEvent, ImportRun } from '../types';
import { toEvent, type ImportEventWire } from './wire';

/**
 * One import being brought over, and the stream that reports it.
 *
 * Lives in a store rather than in the page because an import of eight hundred
 * recipes takes minutes, and a person who taps a recipe halfway through to see
 * whether it really arrived must be able to come back to it.
 */
export class ImportRunner {
  #run = $state<ImportRun | null>(null);

  /** Set while a run is going, so a second tap cannot start a second one. */
  #importing = $state(false);

  /** Why an import could not be started. Not why one went badly. */
  #error = $state<AppError | null>(null);

  /**
   * The stream of the run being followed, and which run it is.
   *
   * Deliberately not `$state`: nothing renders them, and the import they follow
   * carries on whether or not this tab is listening.
   */
  #stream: Stream | null = null;
  #following: { sourceId: string; importId: string } | null = null;

  get run(): ImportRun | null {
    return this.#run;
  }

  get importing(): boolean {
    return this.#importing;
  }

  /** The refusal that stopped an import from starting, if there was one. */
  get error(): AppError | null {
    return this.#error;
  }

  /** Forgets the last refusal, as looking at another library does. */
  clearError(): void {
    this.#error = null;
  }

  /**
   * Asks for a selection to be brought over, and follows it.
   *
   * The request names the work and comes back at once; the recipes arrive
   * afterwards, brought over by the server and reported one at a time over a
   * stream. So the import is not this tab's to finish: the answer already says
   * which cookbook everything is landing on, and closing the laptop costs the
   * progress bar and nothing else.
   */
  async start(
    sourceId: string,
    externalIds: readonly string[],
    anyway?: { readonly cookbookId: string }
  ): Promise<void> {
    if (this.#importing || externalIds.length === 0) {
      return;
    }

    this.#importing = true;
    this.#error = null;

    const result = await request(() =>
      http.POST('/api/v1/recipe-sources/{sourceId}/imports', {
        params: { path: { sourceId } },
        body: {
          externalIds: [...externalIds],
          allowLookalikes: anyway !== undefined,
          cookbookId: anyway?.cookbookId
        }
      })
    );

    if (!result.ok) {
      // Nothing was started, so there is no run to show — the selection is
      // still on screen and the refusal belongs beside the button that made it.
      this.#importing = false;
      this.#error = result.error;

      return;
    }

    this.#run = {
      total: result.value.total,
      done: 0,
      imported: 0,
      skipped: 0,
      failures: [],
      held: [],
      cookbookId: result.value.cookbookId,
      cookbookName: result.value.cookbookName,
      finished: false,
      lost: null
    };

    this.#following = { sourceId, importId: result.value.importId };

    this.#listen();
  }

  /**
   * Brings over the recipes somebody chose from those an import held back.
   *
   * Onto the same shelf as the rest of that import, and only because a person
   * looked at each one beside the recipe it resembles and said so.
   */
  async startAnyway(externalIds: readonly string[]): Promise<void> {
    const following = this.#following;
    const cookbookId = this.#run?.cookbookId;

    if (!following || !cookbookId) {
      return;
    }

    await this.start(following.sourceId, externalIds, { cookbookId });
  }

  /**
   * Picks the stream back up after it was lost.
   *
   * From where it stopped rather than from the beginning: the server numbers
   * every event with how many outcomes it has sent, which is exactly `done`, so
   * asking to resume from there is asking for what this tab is missing and
   * nothing else. The counts on screen stay as they are.
   */
  reconnect(): void {
    if (!this.#following || this.#run === null || this.#run.finished) {
      return;
    }

    this.#run = { ...this.#run, lost: null };

    this.#listen();
  }

  /** Clears a finished run, so the flow can be started again. */
  forget(): void {
    this.#stopListening();
    this.#run = null;
    this.#following = null;
    this.#error = null;
  }

  reset(): void {
    this.forget();
    this.#importing = false;
  }

  #listen(): void {
    const following = this.#following;

    if (!following) {
      return;
    }

    this.#stopListening();
    this.#importing = true;

    this.#stream = watch<ImportEventWire>(
      `/api/v1/recipe-sources/${following.sourceId}/imports/${following.importId}/events`,
      {
        message: (event) => this.#apply(toEvent(event)),
        failed: (error) => {
          // What stopped is this tab's view, not necessarily the import — so
          // the run is kept, the reason is shown, and looking again is offered.
          // Which of the two it was is the error's to say, not this method's.
          this.#stopListening();
          this.#run = this.#run && { ...this.#run, lost: error };
        }
      },
      // Where to resume: the server numbers each event with the number of
      // outcomes it has sent, which is the count already on screen.
      this.#run === null || this.#run.done === 0 ? null : String(this.#run.done)
    );
  }

  #stopListening(): void {
    this.#stream?.close();
    this.#stream = null;
    this.#importing = false;
  }

  /**
   * Folds one event into the run.
   *
   * `done` is taken from the event rather than counted here, so a stream that
   * dropped and resumed cannot leave the bar disagreeing with the server about
   * how far along it is.
   */
  #apply(event: ImportEvent): void {
    const run = this.#run;

    if (!run) {
      return;
    }

    if (event.finished) {
      this.#stopListening();
      this.#run = { ...run, done: event.done, finished: true };

      return;
    }

    if (!event.recipe) {
      // A tick that says nothing has finished yet, sent so the connection
      // survives a slow recipe.
      this.#run = { ...run, done: event.done };

      return;
    }

    const outcome = event.recipe;

    this.#run = {
      ...run,
      done: event.done,
      imported: run.imported + (outcome.outcome === 'imported' ? 1 : 0),
      // Not a failure, and never counted as one: re-running an import is the
      // ordinary way to catch up on what is new.
      skipped: run.skipped + (outcome.outcome === 'already_here' ? 1 : 0),
      failures:
        outcome.outcome === 'failed'
          ? [...run.failures, outcome.title ?? outcome.externalId]
          : run.failures,
      held:
        outcome.outcome === 'looks_like' && outcome.recipeId && outcome.looksLike
          ? [
              ...run.held,
              {
                externalId: outcome.externalId,
                title: outcome.title ?? outcome.externalId,
                recipeId: outcome.recipeId,
                looksLike: outcome.looksLike
              }
            ]
          : run.held
    };
  }
}
