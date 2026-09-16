/**
 * What a cookbook is, to this app.
 *
 * A named shelf that points at recipes. It never carries them: the recipes on
 * it are read through the recipe store with a `cookbookId` filter, which is
 * what lets a cookbook page be the collection page with a different question.
 */
/**
 * What a cookbook that fills itself asks for.
 *
 * Every rule must hold. Nothing here records what matches — that is worked out
 * whenever the shelf is read, which is why a recipe written this evening is on
 * it immediately and no rule change needs anything rebuilt.
 */
export interface CookbookRules {
  /** Tag slugs a recipe must all carry. */
  readonly tags: readonly string[];
  /** Ingredient names a recipe must all use. Matched as substrings. */
  readonly ingredients: readonly string[];
  /** The longest a recipe may take, or null for any length. */
  readonly maxMinutes: number | null;
}

/** Whether somebody chose what is on a shelf, or its rules do. */
export type CookbookKind = 'manual' | 'smart';

export interface Cookbook {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
  readonly kind: CookbookKind;
  /** What it asks for, or null when somebody fills it by hand. */
  readonly rules: CookbookRules | null;
  readonly recipeCount: number;
  /** Up to four photographed recipes for the cover, oldest first. */
  readonly coverRecipeIds: readonly string[];
  readonly updatedAt: string;
}

/** One cookbook, with the version its next write has to quote. */
export interface CookbookDetail extends Cookbook {
  readonly householdId: string;
  readonly version: number;
}

/** A cookbook a recipe is on, as the recipe's own page names it. */
export interface CookbookMembership {
  readonly id: string;
  readonly name: string;
}
