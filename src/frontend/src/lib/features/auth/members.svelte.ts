import { http, request, type AppError } from '$api';
import { registerStore, type LoadStatus } from '$shell/stores';

/** Everyone in a household. Read-only: joining is an invitation and leaving has consequences for a shared library. */
export interface Member {
  readonly userId: string;
  readonly displayName: string;
  readonly role: string;
  readonly joinedAt: string;
}

class Members {
  #items = $state<Member[]>([]);
  #status = $state<LoadStatus>('idle');
  #error = $state<AppError | null>(null);

  /** Whose items these are; plain, not $state: read before the first await of a method an effect calls, so a tracked read would re-trigger it. */
  #householdId: string | null = null;

  get items(): readonly Member[] {
    return this.#items;
  }

  get status(): LoadStatus {
    return this.#status;
  }

  get error(): AppError | null {
    return this.#error;
  }

  async load(householdId: string): Promise<void> {
    // Another kitchen's people are not the ones to show while this one's load.
    if (this.#householdId !== householdId) {
      this.#householdId = householdId;
      this.#items = [];
    }

    this.#status = 'loading';

    const result = await request(() =>
      http.GET('/api/v1/households/{householdId}/members', {
        params: { path: { householdId } }
      })
    );

    if (result.ok) {
      // Longest-standing first: the order a household grew in, stable under the reader.
      this.#items = [...result.value.items].sort((a, b) => a.joinedAt.localeCompare(b.joinedAt));
      this.#status = 'ready';
      this.#error = null;
    } else {
      this.#error = result.error;
      this.#status = 'failed';
    }
  }

  reset(): void {
    this.#householdId = null;
    this.#items = [];
    this.#status = 'idle';
    this.#error = null;
  }
}

export const members = new Members();

// One kitchen's people must not outlive the next sign-in on this device.
registerStore(() => members.reset());
