import { foldText } from '../foldText';
import type { YieldKind } from '../types';

import { parseIngredientLine, type ParsedIngredient } from './parseIngredientLine';

/** A recipe pasted as text, read deterministically; the result is previewed and edited, never applied silently. */
export interface ParsedRecipe {
  readonly sourceUrl?: string;
  /** The first line, when it looks like a name rather than an instruction. */
  readonly title: string;
  readonly ingredients: readonly ParsedIngredient[];
  readonly steps: readonly string[];
  /** Only what the source states: a structured-data site, or a line like "Für 2 Portionen". Guessing would scale every amount wrongly. */
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

const fold = (line: string): string => foldText(bare(line)).replace(/['’]/g, '');

const headingKind = (line: string): 'ingredients' | 'steps' | null => {
  const key = fold(line);

  if (headings.ingredients.includes(key as (typeof headings.ingredients)[number])) {
    return 'ingredients';
  }

  return headings.steps.includes(key as (typeof headings.steps)[number]) ? 'steps' : null;
};

const people = 'portionen|portion|personen|person|pers\\.?|servings?|people';

/** "Für 2 Portionen", "Portionen: 4", "4 servings", "Serves 4": the yield, said on a line of its own. */
const peopleYield = [
  new RegExp(
    `^(?:für|fuer|for|ergibt|macht|makes|serves)\\s*:?\\s*(\\d+)(?:\\s*(?:${people}))?$`,
    'iu'
  ),
  new RegExp(`^(?:${people})\\s*:?\\s*(\\d+)$`, 'iu'),
  new RegExp(`^(\\d+)\\s*(?:${people})$`, 'iu')
];

/** "Ergibt 12 Muffins": a count of things, with the word it was given in. */
const piecesYield = /^(?:ergibt|macht|makes|yields?)\s*:?\s*(\d+)\s+(\p{L}[\p{L}-]*)$/iu;

const timeLabel =
  '(?:zubereitung(?:szeit)?|arbeitszeit|kochzeit|backzeit|gesamtzeit|dauer|zeit|prep(?:aration)? time|cook(?:ing)? time|total time|active time|ready in|time|duration)';

/** "Zubereitung: 20 Minuten", "Gesamtzeit ca. 1 Std. 15 Min.": a label, then only hours and minutes. */
const timeLine = new RegExp(
  `^(${timeLabel})\\s*:?\\s*(?:ca\\.?|circa|etwa|about|approx\\.?)?\\s*(?:(\\d+)\\s*(?:h|std\\.?|stunden?|hours?|hrs?))?\\s*(?:(\\d+)\\s*(?:min\\.?|minuten?|minutes?|mins?))?$`,
  'iu'
);

const totalLabel = /^(?:gesamt|total|ready)/iu;

/** What a line says about the recipe rather than in it; null when it is a line of the recipe. */
type Metadata =
  | { kind: 'servings'; amount: number }
  | { kind: 'pieces'; amount: number; label: string }
  | { kind: 'time'; minutes: number; total: boolean };

const readMetadata = (line: string): Metadata | null => {
  const text = bare(line);

  const counted = peopleYield.map((pattern) => pattern.exec(text)?.[1]).find(Boolean);

  if (Number(counted) > 0) {
    return { kind: 'servings', amount: Number(counted) };
  }

  const pieces = piecesYield.exec(text);

  if (pieces && Number(pieces[1]) > 0) {
    return { kind: 'pieces', amount: Number(pieces[1]), label: pieces[2]! };
  }

  const time = timeLine.exec(text);
  const minutes = Number(time?.[2] ?? 0) * 60 + Number(time?.[3] ?? 0);

  return time && minutes > 0 ? { kind: 'time', minutes, total: totalLabel.test(time[1]!) } : null;
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
  let yielded: Pick<ParsedRecipe, 'servings' | 'yieldKind' | 'yieldLabel'> = {};
  let timed = 0;
  let totalTime = 0;
  // Until a heading says otherwise, each line is judged on its own shape.
  let section: 'ingredients' | 'steps' | null = null;

  for (const line of lines) {
    if (!line) {
      continue;
    }

    // Before anything judges its shape: "Für 2 Portionen" is no title and "Zubereitung: 20 Minuten" is no step.
    const metadata = readMetadata(line);

    if (metadata) {
      if (metadata.kind === 'time') {
        timed += metadata.minutes;
        totalTime = metadata.total ? metadata.minutes : totalTime;
      } else if (yielded.servings === undefined) {
        yielded =
          metadata.kind === 'servings'
            ? { servings: metadata.amount }
            : { servings: metadata.amount, yieldKind: 'pieces', yieldLabel: metadata.label };
      }

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

  // Prep and cook add up unless the page gave the whole itself.
  const minutes = totalTime || timed;

  return {
    title,
    ingredients,
    steps,
    ...yielded,
    ...(minutes > 0 ? { totalMinutes: minutes } : {})
  };
}
