import type { Step } from '../types';

/**
 * Which steps each ingredient belongs to, and which of them name it outright.
 *
 * The editor needs the relationship from both ends. Under a step you say what
 * it needs; beside an ingredient you want to know where it ends up, and whether
 * you forgot to put it anywhere. Both are readings of the same one field, so
 * neither can drift from the other.
 */

/** The ingredients a step's own words name, as opposed to merely needs. */
export function namedIn(step: Step): Set<string> {
  return new Set(
    step.segments
      .filter((segment) => segment.kind === 'ingredient')
      .map((segment) => (segment.kind === 'ingredient' ? segment.ingredientId : ''))
  );
}

/**
 * Ingredient id to the step numbers that need it, counting from one.
 *
 * Numbered rather than indexed because these are shown to a person, and the
 * step above the text already says "Step 1".
 */
export function usageOf(steps: readonly Step[]): Map<string, number[]> {
  const usage = new Map<string, number[]>();

  steps.forEach((step, index) => {
    for (const id of step.uses) {
      const found = usage.get(id);

      if (found) {
        found.push(index + 1);
      } else {
        usage.set(id, [index + 1]);
      }
    }
  });

  return usage;
}

/** Drops an ingredient from every step, for when the line itself is deleted. */
export function withoutIngredients(steps: readonly Step[], removed: ReadonlySet<string>): Step[] {
  return steps.map((step) =>
    step.uses.some((id) => removed.has(id))
      ? { ...step, uses: step.uses.filter((id) => !removed.has(id)) }
      : step
  );
}
