import { registerStore } from '$shell/stores';

/** The tips and the tour this person has seen, by id; see docs/onboarding-design.md. */
export type TipId = 'tour' | 'steps' | 'cook' | 'plan' | 'shopping';

const keyFor = (userId: string) => `culina.tips.v1.${userId}`;

/**
 * What this person has already been shown, kept on the device: a UI preference, nothing private, and a new
 * phone showing the tips once more is no harm. Reads and writes survive storage being unavailable.
 */
class Tips {
  #userId: string | null = null;
  #seen = $state<readonly TipId[]>([]);
  /** The tour asked for: right after the household step of signing up, or from the account menu and Settings. */
  touring = $state(false);

  /** Reads this person's seen tips; a no-op for the same person. */
  own(userId: string | null): void {
    if (userId === this.#userId) return;

    this.#userId = userId;
    this.#seen = userId ? read(keyFor(userId)) : [];
  }

  isNew(id: TipId): boolean {
    return this.#userId !== null && !this.#seen.includes(id);
  }

  dismiss(id: TipId): void {
    if (!this.#userId || this.#seen.includes(id)) return;

    this.#seen = [...this.#seen, id];
    write(keyFor(this.#userId), this.#seen);
  }

  /** Every tip shown once more, from Settings. */
  showAgain(): void {
    if (!this.#userId) return;

    this.#seen = [];
    write(keyFor(this.#userId), []);
  }

  startTour(): void {
    this.touring = true;
  }

  /** Ends the tour for good: closed, skipped or finished alike. */
  endTour(): void {
    this.touring = false;
    this.dismiss('tour');
  }

  reset(): void {
    this.#userId = null;
    this.#seen = [];
    this.touring = false;
  }
}

function read(key: string): TipId[] {
  try {
    const stored: unknown = JSON.parse(localStorage.getItem(key) ?? '[]');

    return Array.isArray(stored)
      ? stored.filter((one): one is TipId => typeof one === 'string')
      : [];
  } catch {
    return [];
  }
}

function write(key: string, seen: readonly TipId[]): void {
  try {
    localStorage.setItem(key, JSON.stringify(seen));
  } catch {
    // Seen for this visit; shown again after a reload, which is harmless.
  }
}

export const tips = new Tips();

registerStore(() => tips.reset());
