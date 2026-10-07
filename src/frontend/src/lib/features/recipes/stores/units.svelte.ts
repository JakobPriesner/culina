import { http, request } from '$api';
import { registerStore } from '$shell/stores';

import { builtInUnits, type Unit } from '../units';

/** The units this kitchen measures in: the thirteen built-ins plus whatever the household's recipes used, so there is no catalogue to maintain. Loaded once per household; the editor adds new ones itself. */
class UnitStore {
  #own = $state<Unit[]>([]);

  /** Which household was asked for; deliberately not `$state`: `load` reads then writes it, so an effect calling it re-ran itself, and a failure (which resets it) became a request per frame. */
  #loadedFor: string | null = null;

  /** Everything a picker should offer, built-in first. */
  get all(): readonly Unit[] {
    return [...builtInUnits, ...this.#own];
  }

  /** Only the ones this household added, which the line parser needs. */
  get own(): readonly Unit[] {
    return this.#own;
  }

  async load(householdId: string): Promise<void> {
    if (!householdId || this.#loadedFor === householdId) {
      return;
    }

    // Claimed before the request so two components mounting together ask once.
    this.#loadedFor = householdId;

    const result = await request(() =>
      http.GET('/api/v1/households/{householdId}/units', {
        params: { path: { householdId } }
      })
    );

    if (result.ok) {
      this.#own = [...result.value.own];

      return;
    }

    // Silent: the built-in units remain, and a household's own unit is merely typed once more.
    this.#loadedFor = null;
  }

  /** Remembers a unit somebody just wrote so the next line reads it as a unit without a reload. */
  remember(unit: Unit): void {
    const known = this.all.some((one) => one.toLowerCase() === unit.toLowerCase());

    if (unit && !known) {
      this.#own = [...this.#own, unit];
    }
  }

  reset(): void {
    this.#own = [];
    this.#loadedFor = null;
  }
}

export const units = new UnitStore();

registerStore(() => units.reset());
