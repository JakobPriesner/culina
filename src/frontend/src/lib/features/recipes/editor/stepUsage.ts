import type { Step, StepSegment } from '../types';

/** Which steps each ingredient belongs to, and which name it outright; both read the same field so they can't drift. */

/** The ingredients a step's own words name, as opposed to merely needs. */
export function namedIn(step: Step): Set<string> {
  return new Set(
    step.segments
      .filter((segment) => segment.kind === 'ingredient')
      .map((segment) => (segment.kind === 'ingredient' ? segment.ingredientId : ''))
  );
}

/**
 * Ingredient id to the 1-based step numbers that need it: `uses` plus everything the words name, as
 * `Step.Create` reads it on the server (`uses` alone said "in no step" beside a just-mentioned ingredient).
 */
export function usageOf(steps: readonly Step[]): Map<string, number[]> {
  const usage = new Map<string, number[]>();

  steps.forEach((step, index) => {
    for (const id of new Set([...step.uses, ...namedIn(step)])) {
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
 * Drops an ingredient from every step. Both halves, because the server unions `uses` with the words
 * ("the words win"): pruning `uses` alone got the id put back. A mention becomes plain text
 * ("Melt @butter" reads "Melt butter"); refusing the delete would mean explaining a forgotten reference.
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

/** Turns mentions of removed ingredients back into plain words, joining neighbouring text. */
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
