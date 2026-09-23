import type { Step, StepSegment } from '../types';

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

/**
 * Drops an ingredient from every step, for when the line itself is deleted.
 *
 * Both halves of the relationship, because the server rebuilds one from the
 * other. `Step.Create` unions what a step lists with everything its sentence
 * names — "the words win, always" — so pruning `uses` while leaving the mention
 * in the prose achieved nothing: the mention put the id straight back, and the
 * next autosave failed with recipes.ingredient_in_use, naming a step the person
 * had not touched.
 *
 * The mention becomes the word it was displaying. "Melt @butter in the pan"
 * reads as "Melt butter in the pan" — the sentence somebody wrote survives, it
 * simply stops pointing at a line that is gone. Refusing the delete instead
 * would mean explaining a reference they may not remember making.
 */
export function withoutIngredients(steps: readonly Step[], removed: ReadonlySet<string>): Step[] {
  return steps.map((step) => {
    const mentioned = step.segments.some(
      (segment) => segment.kind === 'ingredient' && removed.has(segment.ingredientId)
    );

    if (!step.uses.some((id) => removed.has(id)) && !mentioned) {
      return step;
    }

    return {
      ...step,
      uses: step.uses.filter((id) => !removed.has(id)),
      segments: mentioned ? asWords(step.segments, removed) : step.segments
    };
  });
}

/**
 * Turns the mentions of removed ingredients back into plain words.
 *
 * Neighbouring text is joined as it goes, so a sentence does not accumulate a
 * run of fragments every time a line is deleted.
 */
function asWords(segments: readonly StepSegment[], removed: ReadonlySet<string>): StepSegment[] {
  const written: StepSegment[] = [];

  for (const segment of segments) {
    const plain =
      segment.kind === 'ingredient' && removed.has(segment.ingredientId)
        ? { kind: 'text' as const, text: segment.name }
        : segment;

    const previous = written.at(-1);

    if (plain.kind === 'text' && previous?.kind === 'text') {
      written[written.length - 1] = { kind: 'text', text: previous.text + plain.text };
    } else {
      written.push(plain);
    }
  }

  return written;
}
