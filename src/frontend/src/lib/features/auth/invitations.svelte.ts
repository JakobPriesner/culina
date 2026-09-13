import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

/**
 * Invitations to a household, from the inside.
 *
 * An invitation is a bearer code: whoever holds it can join. So it is shown
 * once, where the person who made it can copy it, and it can be taken back
 * before anyone uses it — which is the only remedy for a link sent to the
 * wrong chat.
 */
export interface Invitation {
  readonly invitationId: string;
  readonly createdAt: string;
  readonly expiresAt: string;
  /** Only ever known just after it was made. The server never shows it again. */
  readonly code?: string;
}

type Status = 'idle' | 'loading' | 'ready' | 'failed';

class Invitations {
  #items = $state<Invitation[]>([]);
  #status = $state<Status>('idle');
  #error = $state<AppError | null>(null);

  /** The code of the invitation made in this session, if any. */
  #fresh = $state<string | null>(null);

  get items(): readonly Invitation[] {
    return this.#items;
  }

  get status(): Status {
    return this.#status;
  }

  get error(): AppError | null {
    return this.#error;
  }

  get freshCode(): string | null {
    return this.#fresh;
  }

  async load(householdId: string): Promise<void> {
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

    this.#fresh = result.value.code;
    this.#error = null;

    await this.load(householdId);

    return null;
  }

  async revoke(householdId: string, invitationId: string): Promise<AppError | null> {
    // Gone from the list at once: the person who just decided this was a
    // mistake should not watch a spinner to find out whether it still is one.
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

  reset(): void {
    this.#items = [];
    this.#status = 'idle';
    this.#error = null;
    this.#fresh = null;
  }
}

export const invitations = new Invitations();

// A code shown on screen belongs to whoever was signed in when it was made.
registerStore(() => invitations.reset());
