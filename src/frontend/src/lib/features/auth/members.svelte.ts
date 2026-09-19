import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

/**
 * Everyone in a household.
 *
 * The answer to the question the household settings page asks in its own
 * subtitle — who you cook with — and until now the one thing on it you could
 * not find out. Read-only: joining is an invitation and leaving is a decision
 * with consequences for a shared library, so neither is a row in a list.
 */
export interface Member {
  readonly userId: string;
  readonly displayName: string;
  /** `owner` or `member`, as the server spells it. */
  readonly role: string;
  readonly joinedAt: string;
}

type Status = 'idle' | 'loading' | 'ready' | 'failed';

class Members {
  #items = $state<Member[]>([]);
  #status = $state<Status>('idle');
  #error = $state<AppError | null>(null);

  get items(): readonly Member[] {
    return this.#items;
  }

  get status(): Status {
    return this.#status;
  }

  get error(): AppError | null {
    return this.#error;
  }

  async load(householdId: string): Promise<void> {
    this.#status = 'loading';

    const result = await request(() =>
      http.GET('/api/v1/households/{householdId}/members', {
        params: { path: { householdId } }
      })
    );

    if (result.ok) {
      // Whoever has been here longest first, which is the order a household
      // grew in and the only one that does not change under the reader.
      this.#items = [...result.value.items].sort((a, b) => a.joinedAt.localeCompare(b.joinedAt));
      this.#status = 'ready';
      this.#error = null;
    } else {
      this.#error = result.error;
      this.#status = 'failed';
    }
  }

  reset(): void {
    this.#items = [];
    this.#status = 'idle';
    this.#error = null;
  }
}

export const members = new Members();

// Names of the people in one kitchen do not belong to the next person to sign
// in on this device.
registerStore(() => members.reset());
