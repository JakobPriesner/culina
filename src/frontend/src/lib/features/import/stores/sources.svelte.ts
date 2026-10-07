import { registerStore, type LoadStatus } from '$shell/stores';
import type { AppError } from '$api';

import type { ConnectedSource, ImportRun, SourceRecipe } from '../types';
import { Connections, type SourceDraft } from './connections.svelte';
import { ImportRunner } from './importRun.svelte';
import { LibraryBrowser } from './libraryBrowser.svelte';

/**
 * The libraries connected here, and the one being looked through.
 *
 * The whole of the import's client state, including the progress of a run, in
 * one place for the pages to read: which libraries are connected
 * (`connections.svelte.ts`), the one being looked through
 * (`libraryBrowser.svelte.ts`) and the import being followed
 * (`importRun.svelte.ts`). This is only where the three meet.
 */
class SourceStore {
  #connections = new Connections();
  #library = new LibraryBrowser();
  #runner = new ImportRunner();

  get items(): readonly ConnectedSource[] {
    return this.#connections.items;
  }

  get status(): LoadStatus {
    return this.#connections.status;
  }

  get error(): AppError | null {
    return this.#connections.error;
  }

  get connecting(): boolean {
    return this.#connections.connecting;
  }

  get connectError(): AppError | null {
    return this.#connections.connectError;
  }

  get open(): ConnectedSource | null {
    return this.#library.open;
  }

  get recipes(): readonly SourceRecipe[] {
    return this.#library.recipes;
  }

  get browseStatus(): LoadStatus {
    return this.#library.status;
  }

  get browseError(): AppError | null {
    return this.#library.error;
  }

  get hasMore(): boolean {
    return this.#library.hasMore;
  }

  get loadingMore(): boolean {
    return this.#library.loadingMore;
  }

  /** True while the rest of the library is being fetched to select all of it. */
  get loadingAll(): boolean {
    return this.#library.loadingAll;
  }

  get moreFailed(): boolean {
    return this.#library.moreFailed;
  }

  /** How many they have over there, when that app says. */
  get total(): number | null {
    return this.#library.total;
  }

  get run(): ImportRun | null {
    return this.#runner.run;
  }

  get importing(): boolean {
    return this.#runner.importing;
  }

  /** The refusal that stopped an import from starting, if there was one. */
  get importError(): AppError | null {
    return this.#runner.error;
  }

  list(householdId: string): Promise<void> {
    return this.#connections.list(householdId);
  }

  relist(householdId: string): Promise<void> {
    return this.#connections.relist(householdId);
  }

  connect(draft: SourceDraft): Promise<ConnectedSource | null> {
    return this.#connections.connect(draft);
  }

  /** Forgets a connection, and closes its library if that is the one being looked through. */
  disconnect(sourceId: string): Promise<void> {
    if (this.#library.open?.sourceId === sourceId) {
      this.#library.close();
    }

    return this.#connections.disconnect(sourceId);
  }

  /** Starts looking through one library, or through what matches a search of it. */
  browse(source: ConnectedSource, query = ''): Promise<void> {
    this.#runner.clearError();

    return this.#library.browse(source, query);
  }

  more(): Promise<void> {
    return this.#library.more();
  }

  loadEverything(): Promise<void> {
    return this.#library.loadEverything();
  }

  closeLibrary(): void {
    this.#library.close();
  }

  import(
    sourceId: string,
    externalIds: readonly string[],
    anyway?: { readonly cookbookId: string }
  ): Promise<void> {
    return this.#runner.start(sourceId, externalIds, anyway);
  }

  importAnyway(externalIds: readonly string[]): Promise<void> {
    return this.#runner.startAnyway(externalIds);
  }

  reconnect(): void {
    this.#runner.reconnect();
  }

  forgetRun(): void {
    this.#runner.forget();
  }

  reset(): void {
    this.#connections.reset();
    this.#runner.reset();
    this.#library.close();
  }
}

export const sources = new SourceStore();

registerStore(() => sources.reset());
