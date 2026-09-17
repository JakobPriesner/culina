import type { components } from '$api/generated/schema';

import type { Unit } from './units';

/**
 * What a recipe is, to this app.
 *
 * Derived from the wire shapes but not the same as them. A field renamed on the
 * wire then changes one mapper instead of every component that reads it, and
 * the UI gets names that suit the screen rather than the database.
 */
export type YieldKind = components['schemas']['RecipesRecipeDetail']['yieldKind'];

/** The languages a recipe can be written in, from the contract. */
export type RecipeLanguage = components['schemas']['RecipesRecipeDetail']['language'];

export interface Quantity {
  /** Null when the recipe does not say how much. */
  readonly value: number | null;
  readonly unit: Unit | null;
}

export interface Ingredient {
  readonly id: string;
  readonly quantity: Quantity;
  /** The shoppable noun: "butter". */
  readonly name: string;
  /** The preparation: "finely chopped". */
  readonly note: string | null;
}

export interface IngredientGroup {
  readonly id: string | null;
  /** Null for the implicit first group, which renders as a plain list. */
  readonly name: string | null;
  readonly ingredients: readonly Ingredient[];
}

/**
 * A piece of a step: either words, or a reference to an ingredient.
 *
 * The reference is why a step can say "melt **180 g butter**" at the scaled
 * amount. A step that stored the literal text could not.
 */
export type StepSegment =
  | { readonly kind: 'text'; readonly text: string }
  | {
      readonly kind: 'ingredient';
      readonly ingredientId: string;
      readonly name: string;
      readonly quantity: Quantity;
    };

export interface Step {
  readonly id: string | null;
  readonly segments: readonly StepSegment[];
  /**
   * Everything the step needs, in the recipe's own ingredient order.
   *
   * What you get out before starting it — which is more than the sentence
   * names, because "combine everything and knead" needs five things and says
   * none of them. An ingredient the text does mention is always in here: the
   * server folds the two together, so they cannot come apart.
   */
  readonly uses: readonly string[];
  /** Drives the inline timer, when the step is a wait. */
  readonly durationSeconds: number | null;
}

/** What the list shows. Deliberately smaller than a recipe. */
export interface RecipeSummary {
  readonly id: string;
  readonly title: string;
  readonly imageId: string | null;
  readonly totalMinutes: number | null;
  readonly yieldAmount: number;
  readonly yieldKind: YieldKind;
  readonly tags: readonly string[];
  /** How many times this person has made it. */
  readonly cookCount: number;
  readonly updatedAt: string;
  readonly match: IngredientMatch | null;
}

/** How well a recipe fits what you said you have. */
export interface IngredientMatch {
  readonly matched: number;
  readonly requested: number;
  readonly missing: number;
}

/**
 * Where a recipe came from, when it was not written here.
 *
 * Null for most recipes. An imported recipe is an ordinary recipe in every
 * other respect — edited, cooked, scaled and planned like one somebody typed —
 * and this is the only thing that says otherwise.
 */
export interface RecipeOrigin {
  readonly kind: string;
  readonly sourceId: string | null;
  readonly externalId: string;
  readonly sourceUrl: string | null;
  readonly importedAt: string;
}

export interface Recipe {
  readonly id: string;
  readonly householdId: string;
  readonly title: string;
  readonly description: string | null;
  readonly language: RecipeLanguage;
  readonly yieldAmount: number;
  readonly yieldKind: YieldKind;
  readonly prepMinutes: number | null;
  readonly cookMinutes: number | null;
  readonly totalMinutes: number | null;
  readonly imageId: string | null;
  readonly groups: readonly IngredientGroup[];
  readonly steps: readonly Step[];
  readonly tags: readonly string[];
  readonly origin: RecipeOrigin | null;
  readonly createdBy: string;
  readonly createdAt: string;
  readonly updatedAt: string;
  /** Sent back as `If-Match` on a write, so two editors cannot clobber. */
  readonly version: number;
}

/** Every ingredient in the recipe, flattened, for lookups by id. */
export function ingredientsOf(recipe: Recipe): Map<string, Ingredient> {
  return new Map(
    recipe.groups.flatMap((group) => group.ingredients.map((one) => [one.id, one] as const))
  );
}
