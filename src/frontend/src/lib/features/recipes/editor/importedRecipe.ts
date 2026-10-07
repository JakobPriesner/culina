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
  readonly totalMinutes?: number | null;
}

/**
 * What a website published, in the shape a pasted block of text is read into.
 *
 * Ingredient lines still go through the one line parser, on the side where the
 * person correcting them is. Servings and time are left out unless the site
 * stated them: a guessed number would scale every amount in the recipe.
 */
export function publishedRecipe(page: ImportedPage, ownUnits: readonly string[]): ParsedRecipe {
  return {
    sourceUrl: page.sourceUrl,
    title: page.title ?? '',
    ingredients: page.ingredientLines.map((line) => parseIngredientLine(line, ownUnits)),
    steps: [...page.steps],
    ...(page.servings == null ? {} : { servings: page.servings }),
    ...(page.totalMinutes == null ? {} : { totalMinutes: page.totalMinutes })
  };
}

/** What reading an address came back with: the site's own recipe, or only its words. */
export type PageReading =
  | { sourceUrl: string; transcript: string; words: string }
  | { sourceUrl: string; transcript: string; recipe: ParsedRecipe; outline: string };

/**
 * Reads a recipe from a web page.
 *
 * The server does the fetching — it has to, because a browser cannot read
 * another site — which is why it refuses every address that is not an ordinary
 * public page. Null when it could not be read.
 *
 * A site that publishes nothing structured gives back its words, and those go
 * through the same parser a paste does — one set of heuristics, on the side
 * where the person correcting them is.
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
