/** Rules for a cookbook that fills itself; every rule must hold. Matches are computed on read, so new recipes appear with no rebuild. */
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
  readonly cover: readonly CoverPicture[];
  readonly updatedAt: string;
}

/** One picture on a cover: the recipe it belongs to, and which picture that recipe has now. */
export interface CoverPicture {
  readonly recipeId: string;
  readonly imageId: string;
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
