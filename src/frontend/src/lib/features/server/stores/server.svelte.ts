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

/** What saving did: nothing changed, a restart that came back, or a restart that did not. */
export type SaveOutcome =
  | { readonly kind: 'unchanged' }
  | { readonly kind: 'applied'; readonly setup: Setup }
  | { readonly kind: 'stalled' }
  | { readonly kind: 'failed'; readonly error: AppError };

export type SavePhase = 'idle' | 'saving' | 'restarting';

/**
 * The server and database settings for the instance admin; read on demand, never at boot.
 * Saving answers `204` (nothing differed) or `202` (restarting), and the write is over only once
 * the new host answers.
 */
class ServerStore {
  #server = $state<ServerDraft | null>(null);
  #serverFacts = $state<ServerFacts | null>(null);
  #database = $state<DatabaseDraft | null>(null);
  #databaseFacts = $state<DatabaseFacts | null>(null);
  #status = $state<LoadStatus>('idle');
  // One per read: `load` runs both at once, and the last to finish would decide whether the other
  // failed.
  #serverError = $state<AppError | null>(null);
  #databaseError = $state<AppError | null>(null);
  #phase = $state<SavePhase>('idle');

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

  get status(): LoadStatus {
    return this.#status;
  }

  get error(): AppError | null {
    return this.#serverError ?? this.#databaseError;
  }

  get phase(): SavePhase {
    return this.#phase;
  }

  async load(): Promise<void> {
    this.#status = 'loading';

    await Promise.all([this.loadServer(), this.loadDatabase()]);

    this.#status = this.error ? 'failed' : 'ready';
  }

  async loadServer(): Promise<void> {
    const result = await request(() => http.GET('/api/v1/settings/server'));

    if (result.ok) {
      this.#server = toServerDraft(result.value);
      this.#serverFacts = toServerFacts(result.value);
      this.#serverError = null;
    } else {
      this.#serverError = result.error;
    }
  }

  async loadDatabase(): Promise<void> {
    const result = await request(() => http.GET('/api/v1/settings/database'));

    if (result.ok) {
      this.#database = toDatabaseDraft(result.value);
      this.#databaseFacts = toDatabaseFacts(result.value);
      this.#databaseError = null;
    } else {
      this.#databaseError = result.error;
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

  /** Waits again for a restart that took too long ("check again"). */
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
    this.#serverError = null;
    this.#databaseError = null;
    this.#phase = 'idle';
  }
}

export const server = new ServerStore();

registerStore(() => server.clear());
