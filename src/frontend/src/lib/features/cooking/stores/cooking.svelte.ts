import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

import type { components } from '$api/generated/schema';

/** What this person is cooking now; one session at a time (the database enforces it), shared by phone and laptop. */
export type CookSession = components['schemas']['CookSessionsResponse'];

class CookingStore {
  /** Told which session ended, however it ended, so what is stored for it can go. */
  onEnded: (sessionId: string) => void = () => {};

  #session = $state<CookSession | null>(null);
  #resolved = $state(false);

  /** Coalesces fast step advances. */
  #pendingStep: number | null = null;
  #sending = false;
  #scaleRevision = 0;

  get session(): CookSession | null {
    return this.#session;
  }

  /** Known whether anything is cooking, so the bar can decide. */
  get resolved(): boolean {
    return this.#resolved;
  }

  async resume(): Promise<void> {
    const result = await request(() => http.GET('/api/v1/cook-sessions/current'));

    const previous = this.#session;

    this.#session = result.ok ? result.value : null;

    // Ended on another device since it was last seen.
    if (previous && previous.sessionId !== this.#session?.sessionId) {
      this.onEnded(previous.sessionId);
    }

    this.#resolved = true;
  }

  /** In flight, so concurrent starts share one request: a second POST would abandon the session and restart at step one. */
  #starting: Promise<AppError | null> | null = null;

  async start(
    recipeId: string,
    servings: number,
    householdId: string | null = null
  ): Promise<AppError | null> {
    this.#starting ??= this.#begin(recipeId, servings, householdId);

    try {
      return await this.#starting;
    } finally {
      this.#starting = null;
    }
  }

  /** In the household it is cooked in, which an inherited recipe is not its own. */
  async #begin(
    recipeId: string,
    servings: number,
    householdId: string | null
  ): Promise<AppError | null> {
    const result = await request(() =>
      http.POST('/api/v1/cook-sessions', { body: { recipeId, servings, householdId } })
    );

    if (!result.ok) {
      return result.error;
    }

    this.#session = result.value;
    this.#resolved = true;

    return null;
  }

  /** Moves to a step: applied locally first so Next feels instant, and rapid taps collapse into one request (separate ones could land out of order). */
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

    const revision = ++this.#scaleRevision;
    this.#session = { ...session, servings };

    const result = await request(() =>
      http.PATCH('/api/v1/cook-sessions/{sessionId}', {
        params: { path: { sessionId: session.sessionId } },
        body: { servings }
      })
    );

    // Steps can advance in either view during scaling; adopt only the yield, and only for the latest ask in this session.
    if (this.#session?.sessionId !== session.sessionId || revision !== this.#scaleRevision) return;
    this.#session = {
      ...this.#session,
      servings: result.ok ? result.value.servings : session.servings
    };
  }

  /** Closes the session and says whether the server agreed; local state is dropped either way, so check the answer before reporting success. */
  async end(completed: boolean): Promise<boolean> {
    const session = this.#session;

    this.#session = null;

    if (!session) {
      return true;
    }

    this.onEnded(session.sessionId);

    const result = await request(() =>
      http.DELETE('/api/v1/cook-sessions/{sessionId}', {
        params: { path: { sessionId: session.sessionId }, query: { completed: String(completed) } }
      })
    );

    return result.ok;
  }

  /** Drops the session of a deleted recipe; the server ended it already, so nothing is sent. */
  forget(recipeId: string): void {
    if (this.#session?.recipeId === recipeId) {
      this.onEnded(this.#session.sessionId);
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
