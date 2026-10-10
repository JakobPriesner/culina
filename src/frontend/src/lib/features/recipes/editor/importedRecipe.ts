import { http, request } from '$api';

import { parseIngredientLine } from './parseIngredientLine';
import type { ParsedRecipe } from './parseRecipeText';

/** What the server read off a web page that published structured data. */
export interface ImportedPage {
  readonly sourceUrl: string;
  readonly title?: string | null;
  readonly ingredientLines: readonly string[];
  readonly steps: readonly string[];
  readonly servings?: number | null;
  readonly yieldKind?: string | null;
  readonly yieldLabel?: string | null;
  readonly totalMinutes?: number | null;
}

/** The yield with what it counts and the site's word for it; a kind this client does not know is read as servings, as it always was. */
const yieldOf = (page: ImportedPage, servings: number) => ({
  servings,
  ...(page.yieldKind === 'pieces' ? { yieldKind: 'pieces' as const } : {}),
  ...(page.yieldLabel ? { yieldLabel: page.yieldLabel } : {})
});

/**
 * A site's structured data as a pasted-text reading; servings and time are omitted unless stated,
 * since a guess would scale every amount.
 */
export function publishedRecipe(page: ImportedPage, ownUnits: readonly string[]): ParsedRecipe {
  return {
    sourceUrl: page.sourceUrl,
    title: page.title ?? '',
    ingredients: page.ingredientLines.map((line) => parseIngredientLine(line, ownUnits)),
    steps: [...page.steps],
    ...(page.servings == null ? {} : yieldOf(page, page.servings)),
    ...(page.totalMinutes == null ? {} : { totalMinutes: page.totalMinutes })
  };
}

/** What reading an address came back with: the site's own recipe, or only its words. */
export type PageReading =
  | { sourceUrl: string; transcript: string; words: string }
  | { sourceUrl: string; transcript: string; recipe: ParsedRecipe; outline: string };

/**
 * Reads a recipe from a web page, fetched server-side (a browser cannot read another site); null when unreadable.
 * A site with no structured data returns its words, parsed like a paste.
 */
export async function readRecipePage(
  address: string,
  ownUnits: readonly string[]
): Promise<PageReading | null> {
  const result = await request(() =>
    http.POST('/api/v1/recipe-imports', { body: { url: address } })
  );

  if (!result.ok) {
    return null;
  }

  const draft = result.value;
  const reading = { sourceUrl: draft.sourceUrl, transcript: draft.transcript ?? '' };

  if (draft.text) {
    return { ...reading, words: draft.text };
  }

  return {
    ...reading,
    recipe: publishedRecipe(draft, ownUnits),
    outline: [draft.title, ...draft.ingredientLines, ...draft.steps].filter(Boolean).join('\n')
  };
}
