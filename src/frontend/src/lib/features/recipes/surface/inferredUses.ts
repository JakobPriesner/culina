import { foldedWords } from '../foldText';
import type { Ingredient, Step } from '../types';

/*
 * Which ingredients a step names in its words, for a recipe whose author linked none (no @ mentions):
 * otherwise cook mode would say a step needs nothing while it reads "cook the spaghetti". A guess,
 * so it is only used when the recipe has no links of its own, and never stored.
 *
 * Matching is by word, folded (case, umlauts), in two passes per word of the step:
 * 1. the same word give or take an inflection ending ("Zwiebel" / "Zwiebeln", "Tomate" / "Tomaten");
 * 2. only if nothing matched that way, a compound that starts with it ("Knoblauch" → "Knoblauchzehen"),
 *    so "Tomaten" finds the tomatoes and not the "Tomatenmark" when both are listed.
 */

/** Endings a German or English noun gains in a recipe: plural, case, or a linking -s. */
const endings = new Set(['', 'e', 'n', 'en', 'er', 'es', 's', 'ern', 'nen']);

/** Shorter words are articles and units ("die", "mit", "ml"), never an ingredient on their own. */
const shortest = 4;

const wordsOf = (text: string): string[] => foldedWords(text, shortest);

const inflected = (a: string, b: string): boolean => {
  const [short, long] = a.length <= b.length ? [a, b] : [b, a];

  return long.startsWith(short) && endings.has(long.slice(short.length));
};

const stepWords = (step: Step): string[] =>
  wordsOf(
    step.segments
      .map((segment) => (segment.kind === 'text' ? segment.text : segment.name))
      .join(' ')
  );

/** Ingredient ids per step, in the step order; empty lists where a step names nothing. */
export function inferUses(steps: readonly Step[], ingredients: readonly Ingredient[]): string[][] {
  const named = ingredients.map((ingredient) => ({
    id: ingredient.id,
    words: wordsOf(ingredient.name)
  }));

  return steps.map((step) => {
    const found = new Set<string>();

    for (const word of stepWords(step)) {
      const exact = named.filter((one) => one.words.some((own) => inflected(own, word)));
      const matches =
        exact.length > 0
          ? exact
          : named.filter((one) => one.words.some((own) => own.startsWith(word)));

      for (const one of matches) {
        found.add(one.id);
      }
    }

    return ingredients
      .filter((ingredient) => found.has(ingredient.id))
      .map((ingredient) => ingredient.id);
  });
}
