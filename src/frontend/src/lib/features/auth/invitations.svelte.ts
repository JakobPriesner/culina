import { http, request, type AppError } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

/**
 * Invitations to a household. A code is a bearer credential: shown once for copying, and revocable
 * until used (the remedy for a link sent to the wrong chat).
 */
export interface Invitation {
  readonly invitationId: string;
  readonly createdAt: string;
  readonly expiresAt: string;
  /** Only ever known just after it was made. The server never shows it again. */
  readonly code?: string;
}

class Invitations {
  #items = $state<Invitation[]>([]);
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);

  #fresh = $state<string | null>(null);

  /** Plain, not $state: read before the first await of an effect-called method, where a tracked read would retrigger on its own writes. */
  #householdId: string | null = null;

  get items(): readonly Invitation[] {
    return this.#items;
  }

  get status(): LoadStatus {
    return this.#status;
  }

  get error(): AppError | null {
    return this.#error;
  }

  get freshCode(): string | null {
    return this.#fresh;
  }

  async load(householdId: string): Promise<void> {
    this.#adopt(householdId);
    this.#status = 'loading';

    const result = await request(() =>
      http.GET('/api/v1/households/{householdId}/invitations', {
        params: { path: { householdId } }
      })
    );

    if (result.ok) {
      this.#items = [...result.value.items];
      this.#status = 'ready';
      this.#error = null;
    } else {
      this.#error = result.error;
      this.#status = 'failed';
    }
  }

  async create(householdId: string): Promise<AppError | null> {
    const result = await request(() =>
      http.POST('/api/v1/households/{householdId}/invitations', {
        params: { path: { householdId } }
      })
    );

    if (!result.ok) {
      this.#error = result.error;

      return result.error;
    }

    this.#adopt(householdId);
    this.#fresh = result.value.code;
    this.#error = null;

    await this.load(householdId);

    return null;
  }

  async revoke(householdId: string, invitationId: string): Promise<AppError | null> {
    // Gone from the list at once, so nobody watches a spinner over a mistake.
    const before = this.#items;

    this.#items = this.#items.filter((one) => one.invitationId !== invitationId);

    const result = await request(() =>
      http.DELETE('/api/v1/households/{householdId}/invitations/{invitationId}', {
        params: { path: { householdId, invitationId } }
      })
    );

    if (!result.ok) {
      this.#items = before;
      this.#error = result.error;

      return result.error;
    }

    return null;
  }

  /** Switches to another household's invitations: a link made for one must not show under another's name. */
  #adopt(householdId: string): void {
    if (this.#householdId !== householdId) {
      this.#householdId = householdId;
      this.#items = [];
      this.#fresh = null;
    }
  }

  reset(): void {
    this.#householdId = null;
    this.#items = [];
    this.#status = 'idle';
    this.#error = null;
    this.#fresh = null;
  }
}

export const invitations = new Invitations();

// A code shown on screen belongs to whoever was signed in when it was made.
registerStore(() => invitations.reset());
