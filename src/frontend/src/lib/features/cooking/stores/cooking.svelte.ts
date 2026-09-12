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

  async start(recipeId: string, servings: number): Promise<AppError | null> {
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
  moveTo(index: number): void {
    if (!this.#session) {
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
  async end(completed: boolean): Promise<void> {
    const session = this.#session;

    this.#session = null;

    if (!session) {
      return;
    }

    await request(() =>
      http.DELETE('/api/v1/cook-sessions/{sessionId}', {
        params: { path: { sessionId: session.sessionId }, query: { completed: String(completed) } }
      })
    );
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
