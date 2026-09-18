import { http, request, type AppError } from '$api';

import { toSharedRecipe } from '../mappers';
import type { RecipeReading } from '../types';

/**
 * The one recipe a visitor was sent a link to.
 *
 * Its own store rather than a second mode of the recipe store, which holds a
 * household's library and is emptied when anyone signs out. Nobody signs out of
 * this page: whoever is reading it has no account, and a store registered for
 * that clear-down would be answering a question that is never asked here.
 */
export type SharedStatus = 'idle' | 'loading' | 'ready' | 'failed';

class SharedRecipe {
  /**
   * Which token was last asked for.
   *
   * A plain field and emphatically not `$state`. The page starts this load from
   * an effect, and anything the store *reads* before its first `await` becomes
   * a dependency of that effect — so a store that checked its own status here
   * would be woken by the write it is about to make, and ask again, forever.
   * Reading it back after the await is safe: that is no longer inside the
   * effect.
   */
  #requested: string | null = null;

  #recipe = $state<RecipeReading | null>(null);
  #status = $state<SharedStatus>('idle');
  #error = $state<AppError | null>(null);

  get recipe(): RecipeReading | null {
    return this.#recipe;
  }

  get status(): SharedStatus {
    return this.#status;
  }

  get error(): AppError | null {
    return this.#error;
  }

  async load(token: string): Promise<void> {
    if (this.#requested === token) {
      return;
    }

    this.#requested = token;
    this.#recipe = null;
    this.#status = 'loading';
    this.#error = null;

    const result = await request(() =>
      http.GET('/api/v1/shared-recipes/{token}', { params: { path: { token } } })
    );

    if (this.#requested !== token) {
      return;
    }

    if (!result.ok) {
      this.#error = result.error;
      this.#status = 'failed';

      return;
    }

    this.#recipe = toSharedRecipe(token, result.value);
    this.#status = 'ready';
  }
}

export const sharedRecipe = new SharedRecipe();
