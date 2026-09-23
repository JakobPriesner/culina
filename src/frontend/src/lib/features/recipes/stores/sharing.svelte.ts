import { http, request, type AppError } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

/**
 * Whether one recipe is published behind a link, and what the link is.
 *
 * One recipe at a time, because the question is only ever asked about the
 * recipe on screen — and asking it of the whole library would put a bearer
 * token in the list response for every recipe a household has.
 *
 * Unlike a household invitation, this link is readable again as often as
 * somebody asks. That is deliberate: "what was the address?" is the ordinary
 * question about a link you sent last month, and an answer of "it is gone, here
 * is a new one" would break the one in the message.
 */
class Sharing {
  /** Which recipe the answer below is about. */
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

  /** True while the link is being handed out or taken back. */
  get working(): boolean {
    return this.#working;
  }

  /** The token for this recipe, or null when it is not shared. */
  tokenFor(recipeId: string): string | null {
    return this.#recipeId === recipeId ? this.#token : null;
  }

  /**
   * Asks whether this recipe is shared.
   *
   * A 404 is the ordinary answer and means "not shared" — so it settles the
   * store into `ready` with no link, rather than into `failed`. Read from the
   * status rather than the code, because the two 404s the endpoint can give
   * ("no link" and "no such recipe") are the same answer to the only question
   * the sheet asks. Anything else is a real failure and says so.
   */
  async load(recipeId: string): Promise<void> {
    this.#recipeId = recipeId;
    this.#token = null;
    this.#status = 'loading';
    this.#error = null;

    const result = await request(() =>
      http.GET('/api/v1/recipes/{recipeId}/share', { params: { path: { recipeId } } })
    );

    if (this.#recipeId !== recipeId) {
      return;
    }

    if (result.ok) {
      this.#token = result.value.token;
      this.#status = 'ready';

      return;
    }

    if (result.error.status === 404) {
      this.#status = 'ready';

      return;
    }

    this.#error = result.error;
    this.#status = 'failed';
  }

  /**
   * Publishes the recipe, or returns the link it already had.
   *
   * Idempotent on the server too, so a double tap is free rather than a second
   * link nobody can account for.
   */
  async share(recipeId: string): Promise<AppError | null> {
    this.#working = true;

    const result = await request(() =>
      http.PUT('/api/v1/recipes/{recipeId}/share', { params: { path: { recipeId } } })
    );

    this.#working = false;

    if (!result.ok) {
      this.#error = result.error;

      return result.error;
    }

    this.#recipeId = recipeId;
    this.#token = result.value.token;
    this.#status = 'ready';
    this.#error = null;

    return null;
  }

  /** Takes the link back. Every copy of it stops working at once. */
  async revoke(recipeId: string): Promise<AppError | null> {
    // Gone from the screen at once: somebody who has just decided a link was a
    // mistake should not watch a spinner to find out whether it still is one.
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
