import { inferUses } from '../surface/inferredUses';
import type { Ingredient, Step } from '../types';
import { mentionNamed } from './mentions';
import { namedIn } from './stepUsage';

/**
 * Ingredients a step's words name that it neither mentions nor lists: offered, never added on their
 * own. Matched within this one step, so a later "drain the spaghetti" is spotted too.
 */
export function spottedIn(step: Step, ingredients: readonly Ingredient[]): Ingredient[] {
  const saved = ingredients.filter((one) => one.id);
  const guessed = new Set(inferUses([step], saved)[0]);
  const named = namedIn(step);

  return saved.filter(
    (one) => guessed.has(one.id) && !step.uses.includes(one.id) && !named.has(one.id)
  );
}

/** One spotted ingredient taken in: a mention where the words spell its name (scaled in the sentence), else a need. */
export function adopt(
  step: Step,
  ingredient: Ingredient,
  ingredients: readonly Ingredient[]
): Step {
  const mentioned = mentionNamed(step, ingredient, ingredients);

  return mentioned
    ? { ...step, segments: mentioned }
    : { ...step, uses: [...step.uses, ingredient.id] };
}

/** Every step with all it was spotted naming taken in: the one-tap link after an import. */
export function adoptAllSpotted(
  steps: readonly Step[],
  ingredients: readonly Ingredient[]
): Step[] {
  return steps.map((step) =>
    spottedIn(step, ingredients).reduce((linked, one) => adopt(linked, one, ingredients), step)
  );
}
