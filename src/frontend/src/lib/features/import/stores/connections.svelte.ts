import { http, request, type AppError } from '$api';
import type { LoadStatus } from '$shell/stores';

import type { ConnectedSource } from '../types';
import { toSource } from './wire';

/** What a person types to connect a library. */
export interface SourceDraft {
  householdId: string;
  kind: string;
  address: string;
  label?: string;
  /** Either a token that already exists… */
  token?: string;
  /** …or the sign-in the server trades for one. Never both. */
  username?: string;
  password?: string;
}

/** The libraries a household has connected: listing, connecting and forgetting them. */
export class Connections {
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
