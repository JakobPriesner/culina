import { http, request } from '$api';
import { registerStore } from '$shell/stores';

import { builtInUnits, type Unit } from '../units';

/**
 * The units this kitchen measures in.
 *
 * The thirteen built-in ones, which convert, and whatever else the household's
 * recipes have used. There is no catalogue to maintain: a unit exists because
 * something is measured in it, so the list offered and the recipes written can
 * never disagree.
 *
 * Loaded once per household and kept. It changes only when somebody writes a
 * unit nobody has written before, and the editor adds that one here itself
 * rather than asking the server again.
 */
class UnitStore {
  #own = $state<Unit[]>([]);

  /**
   * Which household has been asked for, deliberately not reactive.
   *
   * Nothing renders it — it exists only so two components mounting together
   * ask once. As `$state` it was a trap: `load` reads it and then writes it,
   * so an `$effect` that called `load` took a dependency on it and re-ran
   * itself, and a failed request — which puts it back to null — turned that
   * into a request per frame until the tab ran out of sockets.
   */
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

    // Nothing is shown about this failing. The built-in units are still there,
    // so the worst case is that a unit this household invented has to be typed
    // out once more — which is not worth a message.
    this.#loadedFor = null;
  }

  /**
   * Remembers a unit somebody just wrote.
   *
   * So the next line they type reads it back as a unit rather than as the first
   * word of an ingredient, without waiting for a save and a reload.
   */
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
