import { http, request, type AppError } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

/**
 * Whether one recipe is published behind a link. One at a time, since a library-wide read would put a bearer token in every list row;
 * unlike an invitation, the link stays readable on demand.
 */
class Sharing {
  #recipeId = $state<string | null>(null);
  #token = $state<string | null>(null);
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);
  #working = $state(false);

  get status(): LoadStatus {
    return this.#status;
  }

  get error(): AppError | null {
    return this.#error;
  }

  get working(): boolean {
    return this.#working;
  }

  tokenFor(recipeId: string): string | null {
    return this.#recipeId === recipeId ? this.#token : null;
  }

  /** Publishes the recipe or returns the link it already had; idempotent on the server, so opening the sheet or a double tap yields the same link. */
  async share(recipeId: string): Promise<AppError | null> {
    this.#recipeId = recipeId;
    this.#token = null;
    this.#status = 'loading';
    this.#error = null;
    this.#working = true;

    const result = await request(() =>
      http.PUT('/api/v1/recipes/{recipeId}/share', { params: { path: { recipeId } } })
    );

    if (this.#recipeId !== recipeId) {
      return null;
    }

    this.#working = false;

    if (!result.ok) {
      this.#error = result.error;
      this.#status = 'failed';

      return result.error;
    }

    this.#token = result.value.token;
    this.#status = 'ready';

    return null;
  }

  /** Takes the link back. Every copy of it stops working at once. */
  async revoke(recipeId: string): Promise<AppError | null> {
    // Cleared at once, so a revoke shows no spinner.
    const before = this.#token;

    this.#token = null;
    this.#working = true;

    const result = await request(() =>
      http.DELETE('/api/v1/recipes/{recipeId}/share', { params: { path: { recipeId } } })
    );

    this.#working = false;

    if (!result.ok) {
      this.#token = before;
      this.#error = result.error;

      return result.error;
    }

    this.#error = null;

    return null;
  }

  reset(): void {
    this.#recipeId = null;
    this.#token = null;
    this.#status = 'idle';
    this.#error = null;
    this.#working = false;
  }
}

export const sharing = new Sharing();

// A link on screen belongs to whoever was signed in when it was asked for.
registerStore(() => sharing.reset());
