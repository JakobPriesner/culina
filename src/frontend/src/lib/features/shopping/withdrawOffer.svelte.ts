import { lineMarks } from './lineMarks.svelte';

export interface UnplannedMeal {
  readonly entryId: string;
  readonly title: string;
}

/**
 * A meal unplanned while the list is beside the week (docs/woche-design.md §5.4): the list asks at the meal's lines
 * whether they go too, instead of a toast at the edge of the screen. One at a time; a newer one replaces it.
 */
class WithdrawOffer {
  #meal = $state<UnplannedMeal | null>(null);

  get meal(): UnplannedMeal | null {
    return this.#meal;
  }

  offer(meal: UnplannedMeal): void {
    if (this.#meal) lineMarks.release(this.#meal.entryId);
    this.#meal = meal;
    lineMarks.hold(meal.entryId);
  }

  /** Answered either way, or no longer askable. */
  settle(): void {
    if (this.#meal) lineMarks.release(this.#meal.entryId);
    this.#meal = null;
  }
}

export const withdrawOffer = new WithdrawOffer();
