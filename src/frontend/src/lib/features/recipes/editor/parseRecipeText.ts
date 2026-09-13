import { parseIngredientLine, type ParsedIngredient } from './parseIngredientLine';

/**
 * Reads a recipe pasted as text.
 *
 * The single biggest reduction in the effort of getting a recipe into the app:
 * almost every recipe arrives as a block of text — a message from a friend, a
 * page copied from a blog, something typed out of a book — and re-typing it
 * line by line is why most of them never get written down at all.
 *
 * Deterministic, and deliberately so. No network call and nothing that guesses
 * differently on Tuesday: the same paste produces the same reading every time,
 * which is what makes correcting it worth the trouble.
 *
 * The parse is never applied silently. What comes back is shown as separate
 * parts and edited before anything is created, because a wrong reading you
 * cannot see is worse than no reading at all.
 */
export interface ParsedRecipe {
  /** The first line, when it looks like a name rather than an instruction. */
  readonly title: string;
  readonly ingredients: readonly ParsedIngredient[];
  readonly steps: readonly string[];
}

/**
 * The headings people actually write, in both languages.
 *
 * Matched on a line of its own, which is what a heading is. "Zubereitung" in
 * the middle of a sentence is a word.
 */
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

/** A number, a fraction glyph, or `1/2` — however a person writes an amount. */
const startsWithAmount = /^[-–—•*\s]*(?:\d|[½⅓⅔¼¾])/u;

/** `1.` or `2)` at the start of a line: a numbered step, not two hundred grams. */
const numberedStep = /^\s*\d{1,2}\s*[.)]\s+\S/;

/**
 * How long a line can be and still plausibly be one ingredient.
 *
 * Counted in words, because characters do not distinguish "40 g Parmesan,
 * frisch gerieben" from "200 g of the flour goes in first, and the rest is
 * folded in at the end" — both are about seventy characters, and only one of
 * them is an ingredient.
 */
const longestIngredient = 8;

/**
 * Whether this line is an ingredient rather than an instruction.
 *
 * An ingredient starts with how much there is of it. The exceptions are the
 * reason this is a function rather than one regular expression: "1. Heat the
 * oven" starts with a number and is a step, and a line long enough to be a
 * sentence is a sentence even if it opens with "200 g of the flour from…".
 */
const looksLikeIngredient = (line: string): boolean =>
  startsWithAmount.test(line) &&
  !numberedStep.test(line) &&
  bare(line).split(/\s+/).length <= longestIngredient &&
  !/[.!?]\s/.test(bare(line));

/** Sentence punctuation, which a copied page's stray labels do not carry. */
const endsLikeSentence = (line: string): boolean => /[.!?…]\s*$/.test(line.trim());

/**
 * Whether this line is a step.
 *
 * Anything left over that reads as a sentence: numbered, or long enough, or
 * punctuated like one. The last of those is what keeps a genuinely terse
 * instruction — "Alles verrühren." — while dropping the "Foto" and "Drucken"
 * that come along when a page is copied out of a browser.
 */
const looksLikeStep = (line: string): boolean =>
  numberedStep.test(line) || bare(line).split(/\s+/).length >= 3 || endsLikeSentence(line);

/** A numbered step keeps its words and loses its number: the list renumbers. */
const stepText = (line: string): string => bare(line).replace(/^\d{1,2}\s*[.)]\s+/, '');

/**
 * Whether the first line is a name rather than the start of the method.
 *
 * A name is short and is not punctuated like a sentence. Somebody who pastes
 * only the method has not given it a title, and taking their first instruction
 * as one would be worse than leaving it blank.
 */
const looksLikeTitle = (line: string): boolean =>
  !endsLikeSentence(line) && bare(line).split(/\s+/).length <= 10;

/** Short enough that a heading is the only reason to call it an ingredient. */
const isShort = (line: string): boolean => bare(line).split(/\s+/).length <= 4;

/**
 * A range is read as its lower bound.
 *
 * "2–3 tbsp oil" becomes two, and the cook adds the third if they want it. The
 * midpoint would invent a precision the recipe never had, and the upper bound
 * is the one you cannot take back out of the pan. It is visible in the preview
 * either way.
 */
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

    // The first thing written, before any heading, is what it is called —
    // unless it is plainly an ingredient or an instruction already.
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

    // Under a heading the heading decides, because it knows what the shape
    // cannot: a bare "Salz" under "Zutaten" is an ingredient with no amount,
    // and the same word under "Zubereitung" is somebody's terse instruction.
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
