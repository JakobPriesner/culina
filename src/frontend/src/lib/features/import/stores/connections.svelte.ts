import { http, request, type AppError } from '$api';
import type { LoadStatus } from '$shell/stores';

import type { ConnectedSource } from '../types';
import { toSource } from './wire';

export interface SourceDraft {
  householdId: string;
  kind: string;
  address: string;
  label?: string;
  /** Either a token… */
  token?: string;
  /** …or the sign-in the server trades for one. Never both. */
  username?: string;
  password?: string;
}

export class Connections {
  #items = $state<ConnectedSource[]>([]);
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);

  /**
   * Household already asked for. Deliberately not `$state`: `list` runs in an `$effect`, and reading
   * reactive state it writes would re-trigger it forever. Claimed before the request so two mounts ask once.
   */
  #listedFor: string | null = null;

  #connecting = $state(false);
  #connectError = $state<AppError | null>(null);

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
      // The guard stays set on failure: `list` asks once per household; retrying is `relist`.
      this.#status = 'failed';
      this.#error = result.error;

      return;
    }

    this.#items = result.value.items.map(toSource);
    this.#status = 'ready';
  }

  async relist(householdId: string): Promise<void> {
    this.#listedFor = null;

    await this.list(householdId);
  }

  /** Connects a library; the server probes it first, so a failure is shown beside the field, not as a toast. */
  async connect(draft: SourceDraft): Promise<ConnectedSource | null> {
    this.#connecting = true;
    this.#connectError = null;

    const result = await request(() =>
      http.POST('/api/v1/recipe-sources', {
        body: {
          householdId: draft.householdId,
          kind: draft.kind,
          address: draft.address,
          ...(draft.label ? { label: draft.label } : {}),
          // Omitted when empty: the server refuses a token and a sign-in together.
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

    // Optimistic: the answer is 204 even for an already-gone source.
    this.#items = this.#items.filter((one) => one.sourceId !== sourceId);

    const result = await request(() =>
      http.DELETE('/api/v1/recipe-sources/{sourceId}', { params: { path: { sourceId } } })
    );

    if (!result.ok) {
      this.#items = before;
      this.#error = result.error;
    }
  }

  reset(): void {
    this.#items = [];
    this.#listedFor = null;
    this.#status = 'idle';
    this.#error = null;
    this.#connecting = false;
    this.#connectError = null;
  }
}
