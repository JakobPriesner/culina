import { http, request, type AppError } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

import { readSetup, waitForRestart } from '../setup';
import {
  toDatabaseDraft,
  toDatabaseFacts,
  toDatabaseRequest,
  toServerDraft,
  toServerFacts,
  toServerRequest,
  type DatabaseDraft,
  type DatabaseFacts,
  type ServerDraft,
  type ServerFacts,
  type Setup
} from '../types';

/**
 * What saving did: nothing (the values were what the server already runs
 * with), a restart that came back, or a restart that did not.
 */
export type SaveOutcome =
  | { readonly kind: 'unchanged' }
  | { readonly kind: 'applied'; readonly setup: Setup }
  | { readonly kind: 'stalled' }
  | { readonly kind: 'failed'; readonly error: AppError };

/** Where a save has got to, for the screen to say. */
export type SavePhase = 'idle' | 'saving' | 'restarting';

/**
 * The server and database settings, for the one account that administers
 * the instance — or, before there is one, for whoever is setting it up.
 *
 * Read on demand, never at boot. Everybody else on the instance would be
 * paying for a request whose answer only one person may see.
 *
 * Saving is not the usual write. The server answers `204` when nothing
 * differed and `202` when it saved the settings and is restarting to use
 * them, and in the second case the write is not over until the new host
 * answers: that is when the settings are in effect, and when it becomes
 * clear whether they started at all.
 */
class ServerStore {
  #server = $state<ServerDraft | null>(null);
  #serverFacts = $state<ServerFacts | null>(null);
  #database = $state<DatabaseDraft | null>(null);
  #databaseFacts = $state<DatabaseFacts | null>(null);
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);
  #phase = $state<SavePhase>('idle');

  /** The server settings as the running process uses them, shaped for a form. */
  get server(): ServerDraft | null {
    return this.#server;
  }

  get serverFacts(): ServerFacts | null {
    return this.#serverFacts;
  }

  get database(): DatabaseDraft | null {
    return this.#database;
  }

  get databaseFacts(): DatabaseFacts | null {
    return this.#databaseFacts;
  }

  /** How far `load` has got. The single reads report only through `error`. */
  get status(): LoadStatus {
    return this.#status;
  }

  /** Why the last read failed. */
  get error(): AppError | null {
    return this.#error;
  }

  get phase(): SavePhase {
    return this.#phase;
  }

  /** Both groups, for the settings screen. */
  async load(): Promise<void> {
    this.#status = 'loading';

    await Promise.all([this.loadServer(), this.loadDatabase()]);

    this.#status = this.#error ? 'failed' : 'ready';
  }

  async loadServer(): Promise<void> {
    const result = await request(() => http.GET('/api/v1/settings/server'));

    if (result.ok) {
      this.#server = toServerDraft(result.value);
      this.#serverFacts = toServerFacts(result.value);
      this.#error = null;
    } else {
      this.#error = result.error;
    }
  }

  async loadDatabase(): Promise<void> {
    const result = await request(() => http.GET('/api/v1/settings/database'));

    if (result.ok) {
      this.#database = toDatabaseDraft(result.value);
      this.#databaseFacts = toDatabaseFacts(result.value);
      this.#error = null;
    } else {
      this.#error = result.error;
    }
  }

  saveServer(draft: ServerDraft): Promise<SaveOutcome> {
    const body = toServerRequest(draft);

    return this.#apply(() => http.PUT('/api/v1/settings/server', { body }));
  }

  saveDatabase(draft: DatabaseDraft): Promise<SaveOutcome> {
    const body = toDatabaseRequest(draft);

    return this.#apply(() => http.PUT('/api/v1/settings/database', { body }));
  }

  /**
   * Waits again for a restart that took longer than it should — the screen's
   * "check again".
   */
  async awaitRestart(): Promise<SaveOutcome> {
    this.#phase = 'restarting';

    const setup = await waitForRestart(null);

    this.#phase = 'idle';

    return setup ? { kind: 'applied', setup } : { kind: 'stalled' };
  }

  async #apply(
    send: () => Promise<{ response: Response; error?: unknown; data?: unknown }>
  ): Promise<SaveOutcome> {
    this.#phase = 'saving';

    // Asked first, so the wait below can tell the new host from this one.
    const before = await readSetup();

    let status = 0;

    const result = await request(async () => {
      const sent = await send();
      status = sent.response.status;

      return sent;
    });

    if (!result.ok) {
      this.#phase = 'idle';

      return { kind: 'failed', error: result.error };
    }

    if (status !== 202) {
      this.#phase = 'idle';

      return { kind: 'unchanged' };
    }

    this.#phase = 'restarting';

    const setup = await waitForRestart(before?.startedAt ?? null);

    this.#phase = 'idle';

    return setup ? { kind: 'applied', setup } : { kind: 'stalled' };
  }

  clear(): void {
    this.#server = null;
    this.#serverFacts = null;
    this.#database = null;
    this.#databaseFacts = null;
    this.#status = 'idle';
    this.#error = null;
    this.#phase = 'idle';
  }
}

export const server = new ServerStore();

registerStore(() => server.clear());
