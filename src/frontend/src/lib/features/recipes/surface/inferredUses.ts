import { foldedWords } from '../foldText';
import type { Ingredient, Step } from '../types';

/*
 * Which ingredients a step names in its words, for a recipe whose author linked none (no @ mentions):
 * otherwise cook mode would say a step needs nothing while it reads "cook the spaghetti". A guess,
 * so it is only used when the recipe has no links of its own, and never stored.
 *
 * Matching is by word, folded (case, umlauts), in three passes per word of the step, each tried only
 * if the one before found nothing:
 * 1. the same word give or take an inflection ending ("Zwiebel" / "Zwiebeln", "Tomate" / "Tomaten");
 * 2. a compound that ends with it, since a German compound is a kind of its last part
 *    ("Zwiebeln" → "Gemüsezwiebel", "Salz" → "Meersalz", "Öl" → "Olivenöl");
 * 3. a compound that starts with it ("Knoblauch" → "Knoblauchzehen"),
 *    so "Tomaten" finds the tomatoes and not the "Tomatenmark" when both are listed.
 */

/** Endings a German or English noun gains in a recipe: plural, case, or a linking -s. */
const endings = new Set(['', 'e', 'n', 'en', 'er', 'es', 's', 'ern', 'nen']);

/** Shorter words are articles and units ("die", "mit", "ml"), never an ingredient on their own. */
const shortest = 4;

/** Short words that are an ingredient after all; never the start of a compound ("Öl" is not "Oliven"). */
const shortIngredients = new Set(['ol']);

const counts = (word: string): boolean => word.length >= shortest || shortIngredients.has(word);

const wordsOf = (text: string): string[] => foldedWords(text).filter(counts);

const inflected = (a: string, b: string): boolean => {
  const [short, long] = a.length <= b.length ? [a, b] : [b, a];

  return long.startsWith(short) && endings.has(long.slice(short.length));
};

/** Whether `own` is a compound whose last part is `word`, inflected or not: "gemusezwiebel" ends with "zwiebeln". */
const headedBy = (own: string, word: string): boolean => {
  for (let start = 1; start < own.length; start++) {
    const last = own.slice(start);

    if (counts(last) && inflected(last, word)) {
      return true;
    }
  }

  return false;
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
      const passes = [
        (own: string) => inflected(own, word),
        (own: string) => headedBy(own, word),
        (own: string) => word.length >= shortest && own.startsWith(word)
      ];
      const matches =
        passes
          .map((pass) => named.filter((one) => one.words.some(pass)))
          .find((hits) => hits.length > 0) ?? [];

      for (const one of matches) {
        found.add(one.id);
      }
    }

    return ingredients
      .filter((ingredient) => found.has(ingredient.id))
      .map((ingredient) => ingredient.id);
  });
}
