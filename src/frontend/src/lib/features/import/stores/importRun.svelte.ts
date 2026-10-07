import { http, request, watch, type AppError, type Stream } from '$api';

import type { ImportEvent, ImportRun } from '../types';
import { toEvent, type ImportEventWire } from './wire';

/** One import being brought over and its event stream; a store so a minutes-long import survives navigating away. */
export class ImportRunner {
  #run = $state<ImportRun | null>(null);

  #importing = $state(false);

  /** Why an import could not be started, not why one went badly. */
  #error = $state<AppError | null>(null);

  /** Not $state: nothing renders them, and the import carries on whether or not this tab listens. */
  #stream: Stream | null = null;
  #following: { sourceId: string; importId: string } | null = null;

  get run(): ImportRun | null {
    return this.#run;
  }

  get importing(): boolean {
    return this.#importing;
  }

  get error(): AppError | null {
    return this.#error;
  }

  clearError(): void {
    this.#error = null;
  }

  /** Requests a selection and follows it; the server does the work, so closing the tab only loses the progress bar. */
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
      // Nothing started: keep the selection on screen and show the refusal beside its button.
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

  /** Brings over recipes a person chose from those an import held back, onto the same shelf. */
  async startAnyway(externalIds: readonly string[]): Promise<void> {
    const following = this.#following;
    const cookbookId = this.#run?.cookbookId;

    if (!following || !cookbookId) {
      return;
    }

    await this.start(following.sourceId, externalIds, { cookbookId });
  }

  /** Resumes the stream from `done` (the server numbers events by outcomes sent); on-screen counts stay. */
  reconnect(): void {
    if (!this.#following || this.#run === null || this.#run.finished) {
      return;
    }

    this.#run = { ...this.#run, lost: null };

    this.#listen();
  }

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
          // Only this tab's view stopped, not necessarily the import: keep the run, show why, offer to look again.
          this.#stopListening();
          this.#run = this.#run && { ...this.#run, lost: error };
        }
      },
      // Resume from the outcome count already on screen.
      this.#run === null || this.#run.done === 0 ? null : String(this.#run.done)
    );
  }

  #stopListening(): void {
    this.#stream?.close();
    this.#stream = null;
    this.#importing = false;
  }

  /** Folds one event into the run; `done` comes from the event so a resumed stream cannot drift from the server. */
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
      // Keep-alive tick; nothing has finished.
      this.#run = { ...run, done: event.done };

      return;
    }

    const outcome = event.recipe;

    this.#run = {
      ...run,
      done: event.done,
      imported: run.imported + (outcome.outcome === 'imported' ? 1 : 0),
      // Not a failure: re-running is the normal way to catch up.
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
