import { http, request, type AppError } from '$api';
import type { LoadStatus } from '$shell/stores';

import type { ConnectedSource, SourceRecipe } from '../types';
import { toRecipe } from './wire';

/** Looking through one connected library, a page at a time. */
export class LibraryBrowser {
  #open = $state<ConnectedSource | null>(null);
  // Raw: up to twenty thousand rows, only ever replaced by the next page's
  // array and never edited in place, so there is nothing for a deep proxy to
  // watch and a lot for it to cost.
  #recipes = $state.raw<SourceRecipe[]>([]);
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);
  #nextPage = $state<string | null>(null);
  #total = $state<number | null>(null);
  #loadingMore = $state(false);
  #loadingAll = $state(false);

  /**
   * Whether the next page could not be read.
   *
   * A list that fetches itself when its end comes into view must stop by
   * itself. Without this a dead connection is a loop: the end of the list stays
   * on screen, asks again, fails again, and keeps asking.
   */
  #moreFailed = $state(false);

  /**
   * What the library being looked through was searched for, and which look
   * this is.
   *
   * A search goes to somebody else's server and can take its time, so the
   * answer to "pa" can arrive after the answer to "pas". Every read notes the
   * generation it was asked under and drops what comes back once another look
   * has begun — otherwise both searches land in one list, and a recipe in both
   * is drawn twice. Not `$state`: nothing renders them.
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

  /** True while the rest of the library is being fetched to select all of it. */
  get loadingAll(): boolean {
    return this.#loadingAll;
  }

  get moreFailed(): boolean {
    return this.#moreFailed;
  }

  /** How many they have over there, when that app says. */
  get total(): number | null {
    return this.#total;
  }

  /** Starts looking through one library, or through what matches a search of it. */
  async browse(source: ConnectedSource, query = ''): Promise<void> {
    this.close();
    this.#open = source;
    this.#query = query;
    this.#status = 'loading';

    await this.#read(source, null);
  }

  /** Reads the next page of the library being looked through. */
  async more(): Promise<void> {
    if (!this.#open || this.#nextPage === null || this.#loadingMore || this.#moreFailed) {
      return;
    }

    const generation = this.#generation;

    this.#loadingMore = true;

    await this.#read(this.#open, this.#nextPage);

    // A page for an earlier look no longer owns the flag: the new look started
    // without it, and may already be reading a page of its own.
    if (generation === this.#generation) {
      this.#loadingMore = false;
    }
  }

  /**
   * Reads the rest of the library, so "all" can mean all of it.
   *
   * Selecting only what happens to be on screen and calling it "all" is a
   * select-all that lies, and the honest fix is not a longer label — it is
   * to go and get the rest. A library of two thousand is twenty reads at a
   * hundred a page, which is a wait worth showing rather than avoiding.
   *
   * Stops at the first page that fails, leaving whatever arrived before it
   * selectable: the failure is already on screen, and a half-read library is
   * better than none.
   */
  async loadEverything(): Promise<void> {
    if (this.#loadingAll || !this.#open) {
      return;
    }

    const generation = this.#generation;

    this.#loadingAll = true;

    // A ceiling, not an expectation. The loop's real end is `hasMore` going
    // false; this is what stops a server that keeps handing back a next page
    // from turning a tick into an afternoon.
    const mostPages = 200;

    for (let page = 0; page < mostPages && this.#nextPage !== null; page += 1) {
      const asked = this.#nextPage;

      await this.more();

      // A new look began: the rest being read is the rest of something no
      // longer on screen, and the flag is the new look's now.
      if (generation !== this.#generation) {
        return;
      }

      // The same token twice means it is not advancing, and asking again would
      // be asking the identical question forever.
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

      // A later page that failed leaves the earlier ones alone. Replacing a
      // screen of recipes somebody is reading with an error, because the page
      // below it could not be read, loses more than it explains.
      this.#moreFailed = true;

      return;
    }

    this.#recipes = this.#recipes.concat(result.value.items.map(toRecipe));
    this.#nextPage = result.value.nextPage ?? null;
    this.#total = result.value.total ?? null;
    this.#status = 'ready';
  }
}
