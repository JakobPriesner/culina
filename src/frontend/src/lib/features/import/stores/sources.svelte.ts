import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

import type { ConnectedSource, ImportOutcome, ImportRun, SourceRecipe } from '../types';

export type LoadStatus = 'idle' | 'loading' | 'ready' | 'failed';

/**
 * How many recipes go in one request.
 *
 * Well under the 25 the server allows, for two reasons that turned out to be
 * the same reason. Progress is only as smooth as the batch is small — a bar
 * that moves once per 25 recipes barely moves — and a batch is not 25 requests
 * to the other server but up to 50 once photos are counted, plus the work of
 * re-encoding each one. At 25 that reliably overran the client's deadline and
 * reported the whole batch as unreadable recipes.
 *
 * Five is small enough to finish comfortably inside that deadline on a slow
 * instance, and small enough that the bar moves while somebody is watching it.
 */
const batchSize = 5;

/**
 * The libraries connected here, and the one being looked through.
 *
 * The whole of the import's client state, including the progress of a run. It
 * lives in a store rather than in the page because an import of eight hundred
 * recipes takes minutes, and a person who taps a recipe halfway through to see
 * whether it really arrived must be able to come back to it.
 */
class SourceStore {
  #items = $state<ConnectedSource[]>([]);
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);

  /**
   * Which household's connections have been asked for.
   *
   * Deliberately **not** `$state`. `list` is called from an `$effect`, and an
   * effect tracks every reactive value read while it runs — so a `list` that
   * read `#items` to decide whether to show a skeleton would depend on the very
   * thing it is about to write, and re-trigger itself forever. That is not a
   * slow page: it is a request per answer until the server starts refusing
   * them. The same trap `cookbooks.svelte.ts` and `units.svelte.ts` document.
   *
   * Keyed by household rather than a bare flag, so switching kitchens still
   * reloads — and claimed before the request, so two components mounting
   * together ask once.
   */
  #listedFor: string | null = null;

  #connecting = $state(false);
  #connectError = $state<AppError | null>(null);

  #open = $state<ConnectedSource | null>(null);
  #recipes = $state<SourceRecipe[]>([]);
  #browseStatus = $state<LoadStatus>('idle');
  #browseError = $state<AppError | null>(null);
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

  #run = $state<ImportRun | null>(null);

  /** Set while a run is going, so a second tap cannot start a second one. */
  #importing = $state(false);

  get items(): readonly ConnectedSource[] {
    return this.#items;
  }

  get status(): LoadStatus {
    return this.#status;
  }

  get error(): AppError | null {
    return this.#error;
  }

  get connecting(): boolean {
    return this.#connecting;
  }

  get connectError(): AppError | null {
    return this.#connectError;
  }

  get open(): ConnectedSource | null {
    return this.#open;
  }

  get recipes(): readonly SourceRecipe[] {
    return this.#recipes;
  }

  get browseStatus(): LoadStatus {
    return this.#browseStatus;
  }

  get browseError(): AppError | null {
    return this.#browseError;
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

  get run(): ImportRun | null {
    return this.#run;
  }

  get importing(): boolean {
    return this.#importing;
  }

  /** What this household has connected. */
  async list(householdId: string): Promise<void> {
    if (this.#listedFor === householdId) {
      return;
    }

    this.#listedFor = householdId;
    this.#status = 'loading';
    this.#error = null;

    const result = await request(() =>
      http.GET('/api/v1/recipe-sources', { params: { query: { householdId } } })
    );

    if (!result.ok) {
      // The guard is deliberately *not* released here. `list` asks at most
      // once per household, full stop — a failure that quietly re-armed it
      // would make the method mean two different things depending on how the
      // last call went, which is exactly the ambiguity this whole guard
      // exists to remove. Asking again is `relist`, and only a person
      // pressing something calls it.
      this.#status = 'failed';
      this.#error = result.error;

      return;
    }

    this.#items = result.value.items.map(toSource);
    this.#status = 'ready';
  }

  /** Asks again after a failure. */
  async relist(householdId: string): Promise<void> {
    this.#listedFor = null;

    await this.list(householdId);
  }

  /**
   * Connects a library, and reports what went wrong when it does not.
   *
   * The server talks to that app before storing anything, so a failure here is
   * a real answer — the address is wrong, or the token is — and is worth
   * showing beside the field rather than as a toast that disappears.
   */
  async connect(draft: {
    householdId: string;
    kind: string;
    address: string;
    label?: string;
    /** Either a token that already exists… */
    token?: string;
    /** …or the sign-in the server trades for one. Never both. */
    username?: string;
    password?: string;
  }): Promise<ConnectedSource | null> {
    this.#connecting = true;
    this.#connectError = null;

    const result = await request(() =>
      http.POST('/api/v1/recipe-sources', {
        body: {
          householdId: draft.householdId,
          kind: draft.kind,
          address: draft.address,
          ...(draft.label ? { label: draft.label } : {}),
          // Sent only when there is one. The server refuses a request that
          // carries both a token and a sign-in, so neither may be padded out
          // with an empty string.
          ...(draft.token ? { token: draft.token } : {}),
          ...(draft.username ? { username: draft.username } : {}),
          ...(draft.password ? { password: draft.password } : {})
        }
      })
    );

    this.#connecting = false;

    if (!result.ok) {
      this.#connectError = result.error;

      return null;
    }

    const connected = toSource(result.value);

    this.#items = [...this.#items, connected];

    return connected;
  }

  /** Forgets a connection. Everything it brought over stays. */
  async disconnect(sourceId: string): Promise<void> {
    const before = this.#items;

    // Optimistic: the row is gone the moment it is tapped, because the answer
    // is 204 either way — disconnecting one that is already gone succeeds.
    this.#items = this.#items.filter((one) => one.sourceId !== sourceId);

    if (this.#open?.sourceId === sourceId) {
      this.closeLibrary();
    }

    const result = await request(() =>
      http.DELETE('/api/v1/recipe-sources/{sourceId}', { params: { path: { sourceId } } })
    );

    if (!result.ok) {
      this.#items = before;
      this.#error = result.error;
    }
  }

  /** Starts looking through one library. */
  async browse(source: ConnectedSource, query?: string): Promise<void> {
    this.#open = source;
    this.#recipes = [];
    this.#nextPage = null;
    this.#total = null;
    this.#browseStatus = 'loading';
    this.#browseError = null;
    this.#moreFailed = false;

    await this.#read(source, null, query);
  }

  /** Reads the next page of the library being looked through. */
  async more(query?: string): Promise<void> {
    if (!this.#open || this.#nextPage === null || this.#loadingMore || this.#moreFailed) {
      return;
    }

    this.#loadingMore = true;

    await this.#read(this.#open, this.#nextPage, query);

    this.#loadingMore = false;
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
  async loadEverything(query?: string): Promise<void> {
    if (this.#loadingAll || !this.#open) {
      return;
    }

    this.#loadingAll = true;

    // A ceiling, not an expectation. The loop's real end is `hasMore` going
    // false; this is what stops a server that keeps handing back a next page
    // from turning a tick into an afternoon.
    const mostPages = 200;

    for (let page = 0; page < mostPages && this.#nextPage !== null; page += 1) {
      const asked = this.#nextPage;

      await this.more(query);

      // The same token twice means it is not advancing, and asking again would
      // be asking the identical question forever.
      if (this.#nextPage === asked) {
        break;
      }
    }

    this.#loadingAll = false;
  }

  closeLibrary(): void {
    this.#open = null;
    this.#recipes = [];
    this.#nextPage = null;
    this.#total = null;
    this.#browseStatus = 'idle';
    this.#browseError = null;
    this.#loadingAll = false;
    this.#moreFailed = false;
  }

  /**
   * Brings a selection over, a batch at a time.
   *
   * The progress is real: a batch that came back is a batch that is done, so
   * nothing here is estimated and nothing has to be reconciled afterwards. The
   * cookbook from the first batch is passed back into every batch after it, so
   * a selection imported in twenty requests lands on one shelf.
   */
  async import(sourceId: string, externalIds: readonly string[]): Promise<void> {
    if (this.#importing || externalIds.length === 0) {
      return;
    }

    this.#importing = true;
    this.#run = {
      total: externalIds.length,
      done: 0,
      imported: 0,
      skipped: 0,
      failures: [],
      cookbookId: null,
      cookbookName: null,
      finished: false
    };

    for (let taken = 0; taken < externalIds.length; taken += batchSize) {
      const batch = externalIds.slice(taken, taken + batchSize);

      const result = await request(() =>
        http.POST('/api/v1/recipe-sources/{sourceId}/imports', {
          params: { path: { sourceId } },
          body: {
            externalIds: [...batch],
            ...(this.#run?.cookbookId ? { cookbookId: this.#run.cookbookId } : {})
          }
        })
      );

      if (!result.ok) {
        // The batch is lost, not the run: everything before it is already
        // written, and asking for the same ids again is a no-op. So the failed
        // batch is counted and the rest goes on.
        this.#record(batch.map(asFailure), null, null);

        continue;
      }

      this.#record(
        result.value.results.map(toOutcome),
        result.value.cookbookId,
        result.value.cookbookName
      );
    }

    this.#run = this.#run === null ? null : { ...this.#run, finished: true };
    this.#importing = false;
  }

  /** Clears a finished run, so the flow can be started again. */
  forgetRun(): void {
    this.#run = null;
  }

  reset(): void {
    this.#items = [];
    this.#listedFor = null;
    this.#status = 'idle';
    this.#error = null;
    this.#connecting = false;
    this.#connectError = null;
    this.#run = null;
    this.#importing = false;
    this.closeLibrary();
  }

  async #read(source: ConnectedSource, page: string | null, query?: string): Promise<void> {
    const result = await request(() =>
      http.GET('/api/v1/recipe-sources/{sourceId}/recipes', {
        params: {
          path: { sourceId: source.sourceId },
          query: {
            ...(page ? { page } : {}),
            ...(query?.trim() ? { query: query.trim() } : {})
          }
        }
      })
    );

    if (!result.ok) {
      if (page === null) {
        this.#browseStatus = 'failed';
        this.#browseError = result.error;

        return;
      }

      // A later page that failed leaves the earlier ones alone. Replacing a
      // screen of recipes somebody is reading with an error, because the page
      // below it could not be read, loses more than it explains.
      this.#moreFailed = true;

      return;
    }

    this.#recipes = [...this.#recipes, ...result.value.items.map(toRecipe)];
    this.#nextPage = result.value.nextPage ?? null;
    this.#total = result.value.total ?? null;
    this.#browseStatus = 'ready';
  }

  #record(results: readonly ImportOutcome[], cookbookId: string | null, name: string | null): void {
    const run = this.#run;

    if (!run) {
      return;
    }

    this.#run = {
      ...run,
      done: run.done + results.length,
      imported: run.imported + results.filter((one) => one.outcome === 'imported').length,
      skipped: run.skipped + results.filter((one) => one.outcome === 'already_here').length,
      failures: [
        ...run.failures,
        ...results
          .filter((one) => one.outcome === 'failed')
          .map((one) => one.title ?? one.externalId)
      ],
      cookbookId: run.cookbookId ?? cookbookId,
      cookbookName: run.cookbookName ?? name
    };
  }
}

const toSource = (wire: {
  sourceId: string;
  kind: string;
  label: string;
  address: string;
  createdAt: string;
  lastUsedAt?: string | null;
}): ConnectedSource => ({
  sourceId: wire.sourceId,
  kind: wire.kind as ConnectedSource['kind'],
  label: wire.label,
  address: wire.address,
  createdAt: wire.createdAt,
  lastUsedAt: wire.lastUsedAt ?? null
});

const toRecipe = (wire: {
  externalId: string;
  title: string;
  description?: string | null;
  totalMinutes?: number | null;
  alreadyHere?: string | null;
}): SourceRecipe => ({
  externalId: wire.externalId,
  title: wire.title,
  description: wire.description ?? null,
  totalMinutes: wire.totalMinutes ?? null,
  alreadyHere: wire.alreadyHere ?? null
});

const toOutcome = (wire: {
  externalId: string;
  outcome: string;
  recipeId?: string | null;
  title?: string | null;
  reason?: string | null;
}): ImportOutcome => ({
  externalId: wire.externalId,
  outcome: wire.outcome as ImportOutcome['outcome'],
  recipeId: wire.recipeId ?? null,
  title: wire.title ?? null,
  reason: wire.reason ?? null
});

const asFailure = (externalId: string): ImportOutcome => ({
  externalId,
  outcome: 'failed',
  recipeId: null,
  title: null,
  reason: null
});

export const sources = new SourceStore();

registerStore(() => sources.reset());
