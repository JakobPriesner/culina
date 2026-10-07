import { http, request, type AppError } from '$api';

import { toSharedRecipe } from '../mappers';
import type { RecipeReading } from '../types';
import { type LoadStatus } from '$shell/stores';

/** The one recipe a visitor was sent a link to; its own store because the library store is cleared on sign-out and nobody signs out of this page. */
class SharedRecipe {
  /** Last token asked for. Plain, not `$state`: the page calls this from an effect, and anything read before the first `await` becomes
   * a dependency, so the store's own write would re-trigger it forever. */
  #requested: string | null = null;

  #recipe = $state<RecipeReading | null>(null);
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);

  get recipe(): RecipeReading | null {
    return this.#recipe;
  }

  get status(): LoadStatus {
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
