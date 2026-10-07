import { http, request, type AppError } from '$api';
import type { LoadStatus } from '$shell/stores';

import type { ConnectedSource, SourceRecipe } from '../types';
import { toRecipe } from './wire';

/** Looking through one connected library, a page at a time. */
export class LibraryBrowser {
  #open = $state<ConnectedSource | null>(null);
  // Raw: up to twenty thousand rows, only ever replaced wholesale.
  #recipes = $state.raw<SourceRecipe[]>([]);
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);
  #nextPage = $state<string | null>(null);
  #total = $state<number | null>(null);
  #loadingMore = $state(false);
  #loadingAll = $state(false);

  /** Set when the next page failed, so a list that fetches at its end stops instead of looping. */
  #moreFailed = $state(false);

  /**
   * `#generation` drops answers from superseded looks (a slow "pa" landing after "pas"). Not
   * `$state`: nothing renders them.
   */
  #query = '';
  #generation = 0;

  get open(): ConnectedSource | null {
    return this.#open;
  }

  get recipes(): readonly SourceRecipe[] {
    return this.#recipes;
  }

  get status(): LoadStatus {
    return this.#status;
  }

  get error(): AppError | null {
    return this.#error;
  }

  get hasMore(): boolean {
    return this.#nextPage !== null;
  }

  get loadingMore(): boolean {
    return this.#loadingMore;
  }

  get loadingAll(): boolean {
    return this.#loadingAll;
  }

  get moreFailed(): boolean {
    return this.#moreFailed;
  }

  /** Total in the source app, when it says. */
  get total(): number | null {
    return this.#total;
  }

  async browse(source: ConnectedSource, query = ''): Promise<void> {
    this.close();
    this.#open = source;
    this.#query = query;
    this.#status = 'loading';

    await this.#read(source, null);
  }

  async more(): Promise<void> {
    if (!this.#open || this.#nextPage === null || this.#loadingMore || this.#moreFailed) {
      return;
    }

    const generation = this.#generation;

    this.#loadingMore = true;

    await this.#read(this.#open, this.#nextPage);

    // A superseded look no longer owns the flag.
    if (generation === this.#generation) {
      this.#loadingMore = false;
    }
  }

  /**
   * Reads every remaining page so "select all" means all of it.
   * Stops at the first failed page, leaving the earlier ones selectable.
   */
  async loadEverything(): Promise<void> {
    if (this.#loadingAll || !this.#open) {
      return;
    }

    const generation = this.#generation;

    this.#loadingAll = true;

    // Ceiling against a server that keeps handing back a next page.
    const mostPages = 200;

    for (let page = 0; page < mostPages && this.#nextPage !== null; page += 1) {
      const asked = this.#nextPage;

      await this.more();

      // A new look began; the flag is its now.
      if (generation !== this.#generation) {
        return;
      }

      // Same token twice: not advancing.
      if (this.#nextPage === asked) {
        break;
      }
    }

    this.#loadingAll = false;
  }

  close(): void {
    this.#generation += 1;
    this.#open = null;
    this.#query = '';
    this.#recipes = [];
    this.#nextPage = null;
    this.#total = null;
    this.#status = 'idle';
    this.#error = null;
    this.#loadingMore = false;
    this.#loadingAll = false;
    this.#moreFailed = false;
  }

  async #read(source: ConnectedSource, page: string | null): Promise<void> {
    const generation = this.#generation;
    const query = this.#query.trim();

    const result = await request(() =>
      http.GET('/api/v1/recipe-sources/{sourceId}/recipes', {
        params: {
          path: { sourceId: source.sourceId },
          query: {
            ...(page ? { page } : {}),
            ...(query ? { query } : {})
          }
        }
      })
    );

    if (generation !== this.#generation) {
      return;
    }

    if (!result.ok) {
      if (page === null) {
        this.#status = 'failed';
        this.#error = result.error;

        return;
      }

      // A failed later page keeps the earlier ones on screen rather than replacing them with an
      // error.
      this.#moreFailed = true;

      return;
    }

    this.#recipes = this.#recipes.concat(result.value.items.map(toRecipe));
    this.#nextPage = result.value.nextPage ?? null;
    this.#total = result.value.total ?? null;
    this.#status = 'ready';
  }
}
