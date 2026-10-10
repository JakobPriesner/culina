import type { YieldKind } from '../types';

import { parseIngredientLine, type ParsedIngredient } from './parseIngredientLine';

/** A recipe pasted as text, read deterministically; the result is previewed and edited, never applied silently. */
export interface ParsedRecipe {
  readonly sourceUrl?: string;
  /** The first line, when it looks like a name rather than an instruction. */
  readonly title: string;
  readonly ingredients: readonly ParsedIngredient[];
  readonly steps: readonly string[];
  /** Only structured-data sites know these; guessing would scale every amount wrongly. */
  readonly servings?: number;
  /** What `servings` counts, when the site said: people, or things ("12 Muffins"). Only beside `servings`. */
  readonly yieldKind?: YieldKind;
  /** The site's own word for what it makes, as it wrote it ("Muffins"); only beside `servings`. */
  readonly yieldLabel?: string;
  readonly totalMinutes?: number;
}

/** Headings in both languages, matched on a line of their own ("Zubereitung" mid-sentence is a word). */
const headings = {
  ingredients: [
    'zutaten',
    'ingredients',
    'du brauchst',
    'you will need',
    'you need',
    'einkaufsliste'
  ],
  steps: [
    'zubereitung',
    'anleitung',
    'so gehts',
    'so geht es',
    'schritte',
    'steps',
    'method',
    'instructions',
    'preparation',
    'directions'
  ]
} as const;

/** Trailing punctuation and list bullets a heading or a line may carry. */
const bare = (line: string): string =>
  line
    .replace(/^[-–—•*\s]+/, '')
    .replace(/[:：.\s]+$/, '')
    .trim();

const fold = (line: string): string =>
  bare(line)
    .toLowerCase()
    .replaceAll('ä', 'a')
    .replaceAll('ö', 'o')
    .replaceAll('ü', 'u')
    .replaceAll('ß', 'ss')
    .replace(/['’]/g, '');

const headingKind = (line: string): 'ingredients' | 'steps' | null => {
  const key = fold(line);

  if (headings.ingredients.includes(key as (typeof headings.ingredients)[number])) {
    return 'ingredients';
  }

  return headings.steps.includes(key as (typeof headings.steps)[number]) ? 'steps' : null;
};

const startsWithAmount = /^[-–—•*\s]*(?:\d|[½⅓⅔¼¾])/u;

/** `1.` or `2)` at the start of a line: a numbered step, not two hundred grams. */
const numberedStep = /^\s*\d{1,2}\s*[.)]\s+\S/;

/** Max words in an ingredient line; characters cannot tell "40 g Parmesan" from a sentence. */
const longestIngredient = 8;

/** An ingredient starts with an amount; numbered steps and sentence-length lines are excluded. */
const looksLikeIngredient = (line: string): boolean =>
  startsWithAmount.test(line) &&
  !numberedStep.test(line) &&
  bare(line).split(/\s+/).length <= longestIngredient &&
  !/[.!?]\s/.test(bare(line));

const endsLikeSentence = (line: string): boolean => /[.!?…]\s*$/.test(line.trim());

/** Anything left that reads as a sentence; punctuation keeps terse steps ("Alles verrühren.") and drops copied-page labels ("Foto"). */
const looksLikeStep = (line: string): boolean =>
  numberedStep.test(line) || bare(line).split(/\s+/).length >= 3 || endsLikeSentence(line);

/** A numbered step keeps its words and loses its number: the list renumbers. */
const stepText = (line: string): string => bare(line).replace(/^\d{1,2}\s*[.)]\s+/, '');

/** A name is short and not sentence-punctuated; a first instruction must not become the title. */
const looksLikeTitle = (line: string): boolean =>
  !endsLikeSentence(line) && bare(line).split(/\s+/).length <= 10;

/** Short enough that a heading is the only reason to call it an ingredient. */
const isShort = (line: string): boolean => bare(line).split(/\s+/).length <= 4;

/** A range reads as its lower bound: a midpoint invents precision, the upper bound can't be taken back out of the pan. */
const lowerBound = (line: string): string =>
  line.replace(/^(\s*[-–—•*]?\s*)(\d+(?:[.,]\d+)?)\s*[-–—]\s*\d+(?:[.,]\d+)?/u, '$1$2');

export function parseRecipeText(text: string, ownUnits: readonly string[] = []): ParsedRecipe {
  const lines = (text ?? '').split(/\r?\n/).map((line) => line.trim());

  const ingredients: ParsedIngredient[] = [];
  const steps: string[] = [];
  let title = '';
  // Until a heading says otherwise, each line is judged on its own shape.
  let section: 'ingredients' | 'steps' | null = null;

  for (const line of lines) {
    if (!line) {
      continue;
    }

    const heading = headingKind(line);

    if (heading) {
      section = heading;
      continue;
    }

    // The first line before any heading is the title, unless it already looks like an ingredient or step.
    if (
      !title &&
      section === null &&
      looksLikeTitle(line) &&
      !looksLikeIngredient(line) &&
      !numberedStep.test(line)
    ) {
      title = bare(line);
      continue;
    }

    // Under a heading the heading decides: bare "Salz" is an ingredient under "Zutaten", a step under "Zubereitung".
    const isIngredient =
      section === 'steps'
        ? false
        : section === 'ingredients'
          ? !numberedStep.test(line) && (looksLikeIngredient(line) || isShort(line))
          : looksLikeIngredient(line);

    if (isIngredient) {
      ingredients.push(parseIngredientLine(lowerBound(bare(line)), ownUnits));
      continue;
    }

    if (looksLikeStep(line)) {
      steps.push(stepText(line));
    }
  }

  return { title, ingredients, steps };
}
