/**
 * Beside the list (docs/woche-design.md §5.3): which meal, or which line, a mouse or the keyboard rests on, so the
 * other side lights up what belongs to it. Pure highlighting: nothing is kept once the pointer moves on.
 */
class Provenance {
  #meal = $state<string | null>(null);
  #lineMeals = $state<readonly string[]>([]);

  /** The planned meal pointed at, whose lines are lit in the list. */
  get meal(): string | null {
    return this.#meal;
  }

  /** The planned meals of the line pointed at, whose cards are lit in the week. */
  get lineMeals(): readonly string[] {
    return this.#lineMeals;
  }

  pointMeal(entryId: string | null): void {
    this.#meal = entryId;
  }

  pointLine(entryIds: readonly string[]): void {
    this.#lineMeals = entryIds;
  }
}

export const provenance = new Provenance();
