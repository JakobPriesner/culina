import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

import type { components } from '$api/generated/schema';

/**
 * What this person is cooking, right now.
 *
 * One session at a time, because the phone against the mixing bowl and the
 * laptop on the counter are the same cook — the database enforces it, and this
 * store simply holds the one answer.
 */
export type CookSession = components['schemas']['CookSessionsResponse'];

class CookingStore {
  #session = $state<CookSession | null>(null);
  #resolved = $state(false);

  /** Coalesces the step advances a fast cook produces. */
  #pendingStep: number | null = null;
  #sending = false;

  get session(): CookSession | null {
    return this.#session;
  }

  /** True once we know whether anything is cooking, so the bar can decide. */
  get resolved(): boolean {
    return this.#resolved;
  }

  /** Reads the one active session, if there is one. Called on boot. */
  async resume(): Promise<void> {
    const result = await request(() => http.GET('/api/v1/cook-sessions/current'));

    this.#session = result.ok ? result.value : null;
    this.#resolved = true;
  }

  /**
   * Begins cooking, once.
   *
   * Shared by concurrent callers rather than started twice. The cook screen
   * asks in an effect, and an effect can run again before the first answer
   * arrives — a second POST abandons the session the first one made and comes
   * back at step one, which lands on somebody who had already tapped Next.
   */
  /** In flight, so a second ask joins the first rather than starting again. */
  #starting: Promise<AppError | null> | null = null;

  async start(recipeId: string, servings: number): Promise<AppError | null> {
    this.#starting ??= this.#begin(recipeId, servings);

    try {
      return await this.#starting;
    } finally {
      this.#starting = null;
    }
  }

  async #begin(recipeId: string, servings: number): Promise<AppError | null> {
    const result = await request(() =>
      http.POST('/api/v1/cook-sessions', { body: { recipeId, servings } })
    );

    if (!result.ok) {
      return result.error;
    }

    this.#session = result.value;
    this.#resolved = true;

    return null;
  }

  /**
   * Moves to a step.
   *
   * Applied here first and sent after: tapping "next" must feel instant, and
   * the server's answer changes nothing the cook can see. Rapid taps collapse
   * into one request for the step they landed on — sending four requests for
   * four taps would deliver them out of order and land the cook somewhere they
   * were not.
   */
  moveTo(recipeId: string, index: number): void {
    if (this.#session?.recipeId !== recipeId) {
      return;
    }

    this.#session = { ...this.#session, currentStepIndex: index };
    this.#pendingStep = index;

    if (this.#sending) {
      return;
    }

    this.#sending = true;

    void this.#flushStep().finally(() => {
      this.#sending = false;
    });
  }

  async rescale(servings: number): Promise<void> {
    const session = this.#session;

    if (!session) {
      return;
    }

    this.#session = { ...session, servings };

    const result = await request(() =>
      http.PATCH('/api/v1/cook-sessions/{sessionId}', {
        params: { path: { sessionId: session.sessionId } },
        body: { servings }
      })
    );

    if (result.ok) {
      this.#session = result.value;
    } else {
      // Put back exactly what was there: an amount that silently failed to save
      // is worse than one that visibly did not change.
      this.#session = session;
    }
  }

  /** Finishing and giving up are both "over", but only one means it worked. */
  /**
   * Closes the session, and says whether the server agreed.
   *
   * The local session is dropped either way — whoever pressed "I made it" is
   * finished cooking whatever the network thinks. The answer is returned
   * because the page reports an outcome to the person, and reporting one
   * without looking at this is how "Added to your cooking history" appeared
   * over a request that had failed.
   */
  async end(completed: boolean): Promise<boolean> {
    const session = this.#session;

    this.#session = null;

    if (!session) {
      return true;
    }

    const result = await request(() =>
      http.DELETE('/api/v1/cook-sessions/{sessionId}', {
        params: { path: { sessionId: session.sessionId }, query: { completed: String(completed) } }
      })
    );

    return result.ok;
  }

  /**
   * Lets go of the session for a recipe that has been deleted.
   *
   * The server ended it along with the recipe, so there is nothing to send;
   * the bar offering to resume it is all that is left, and it would resume
   * onto nothing.
   */
  forget(recipeId: string): void {
    if (this.#session?.recipeId === recipeId) {
      this.#session = null;
      this.#pendingStep = null;
    }
  }

  reset(): void {
    this.#session = null;
    this.#resolved = false;
    this.#pendingStep = null;
    this.#sending = false;
  }

  async #flushStep(): Promise<void> {
    while (this.#pendingStep !== null && this.#session) {
      const index = this.#pendingStep;
      const sessionId = this.#session.sessionId;

      this.#pendingStep = null;

      await request(() =>
        http.PATCH('/api/v1/cook-sessions/{sessionId}', {
          params: { path: { sessionId } },
          body: { currentStepIndex: index }
        })
      );
    }
  }
}

export const cooking = new CookingStore();

registerStore(() => cooking.reset());
