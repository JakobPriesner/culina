import type { NutritionValue } from '$features/nutrition/types';

import type { components } from '$api/generated/schema';

import type { Unit } from './units';

/** The app's recipe shapes, derived from the wire types so a wire rename changes one mapper. */
export type YieldKind = components['schemas']['RecipesRecipeDetail']['yieldKind'];

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

/** A step piece: words, or an ingredient reference so the step shows the scaled amount. */
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
  /** The step's name, or null to show its number. */
  readonly title: string | null;
  readonly segments: readonly StepSegment[];
  /** Everything the step needs, including ingredients its text never names; the server merges both. */
  readonly uses: readonly string[];
  /** Drives the inline timer, when the step is a wait. */
  readonly durationSeconds: number | null;
}

/** What the list shows; smaller than a recipe. */
export interface RecipeSummary {
  /** Per serving or piece; absent in older offline responses. */
  readonly calories?: NutritionValue | null;
  readonly id: string;
  /** The owning household, when another than the one viewed (inherited recipe). */
  readonly householdId?: string;
  readonly title: string;
  readonly imageId: string | null;
  readonly totalMinutes: number | null;
  readonly yieldAmount: number;
  readonly yieldKind: YieldKind;
  /** The recipe's own word for what it makes, or null for the usual one. */
  readonly yieldLabel: string | null;
  readonly tags: readonly string[];
  readonly cookCount: number;
  /** When this person last made it, or null. */
  readonly lastCookedAt: string | null;
  readonly updatedAt: string;
  readonly match: IngredientMatch | null;
  /** Why it answers a search its title does not name; null for a title match. */
  readonly matchReason?: MatchReason | null;
  /** The diet a search asked for, when this recipe only keeps it because nothing refutes it. */
  readonly presumedDiet?: PresumableDiet | null;
}

/** The diets an ingredient list can refute, and so the only ones ever presumed. */
export type PresumableDiet = 'vegetarian' | 'vegan';

/** Why a recipe is in a search it does not name; `concept` always carries a reason so it never looks like a real match. */
export interface MatchReason {
  readonly kind: 'ingredient' | 'tag' | 'text' | 'concept';
  /** The ingredient or tag as the recipe writes it, or the concept. */
  readonly term: string | null;
}

export type ChipKind = 'time' | 'quick' | 'diet' | 'meal' | 'cuisine' | 'ingredient' | 'exclusion';

/** One thing the server read a query to mean, and the characters (`start`–`end`) it read it from. */
export interface SearchChip {
  readonly kind: ChipKind;
  /** Minutes, a stable key (`vegetarian`, `dinner`), or a word as typed. */
  readonly value: string;
  readonly text: string;
  readonly start: number;
  readonly end: number;
  readonly word: string | null;
}

/** A query, as the server understood it. */
export interface Interpretation {
  readonly freeText: string;
  readonly chips: readonly SearchChip[];
  /** What was typed, when the words were corrected. */
  readonly correctedFrom: string | null;
  /** Readings set aside because nothing matched all of them. */
  readonly relaxed: readonly SearchChip[];
  /** Two readings that rule each other out. */
  readonly conflict: readonly SearchChip[];
}

export interface Facet {
  readonly value: string;
  readonly label: string | null;
  readonly count: number;
}

export interface Facets {
  readonly tags: readonly Facet[];
  readonly times: readonly Facet[];
  readonly cuisines: readonly Facet[];
}

export type Completion =
  | {
      readonly kind: 'recipe';
      readonly label: string;
      readonly recipeId: string;
      readonly imageId: string | null;
      readonly totalMinutes: number | null;
    }
  | { readonly kind: 'ingredient'; readonly label: string; readonly recipeCount: number }
  | {
      readonly kind: 'tag';
      readonly label: string;
      readonly slug: string;
      readonly recipeCount: number;
    }
  | {
      readonly kind: 'refinement';
      readonly label: string;
      readonly recipeCount: number;
      readonly maxMinutes: number;
    };

/** Why a recipe was suggested; null when no single signal decided. The server sends a code, never a sentence: wording is the client's. */
export type SuggestionReasonCode =
  | 'affinity'
  | 'rediscovery'
  | 'tag'
  | 'ingredient'
  | 'season'
  | 'slot'
  | 'household'
  | 'fresh'
  | 'similar';

export interface SuggestionReason {
  readonly code: SuggestionReasonCode;
  /** The tag, ingredient or person it is about. */
  readonly subject: string | null;
}

export interface Suggestion extends RecipeSummary {
  readonly reason: SuggestionReason | null;
}

/** Why a recipe is shown beside another: up to three shared kinds or ingredients, in the server's language. */
export interface RelatedReason {
  readonly kind: 'kinds' | 'ingredients';
  readonly shared: readonly string[];
}

export interface RelatedRecipe extends RecipeSummary {
  readonly reason: RelatedReason;
}

export interface IngredientMatch {
  readonly matched: number;
  readonly requested: number;
  readonly missing: number;
}

/** A recipe as read: what a shared link exposes and nothing about whose kitchen it is, so the visitor surface cannot reach private fields. */
export interface RecipeReading {
  readonly id: string;
  readonly title: string;
  readonly description: string | null;
  readonly language: RecipeLanguage;
  readonly yieldAmount: number;
  readonly yieldKind: YieldKind;
  readonly yieldLabel: string | null;
  readonly prepMinutes: number | null;
  readonly cookMinutes: number | null;
  readonly totalMinutes: number | null;
  readonly imageId: string | null;
  readonly groups: readonly IngredientGroup[];
  readonly steps: readonly Step[];
  readonly tags: readonly string[];
  /** Where it was originally published, when imported; the only trace an import leaves. */
  readonly sourceUrl: string | null;
  readonly updatedAt: string;
}

/** The same recipe, to the household that keeps it. */
export interface Recipe extends RecipeReading {
  readonly householdId: string;
  readonly createdBy: string;
  readonly createdAt: string;
  /** Sent back as `If-Match` on a write. */
  readonly version: number;
}

/** Every ingredient, flat in written order. */
export const everyIngredient = (recipe: RecipeReading): readonly Ingredient[] =>
  recipe.groups.flatMap((group) => group.ingredients);

export function ingredientsOf(recipe: RecipeReading): Map<string, Ingredient> {
  return new Map(everyIngredient(recipe).map((one) => [one.id, one] as const));
}
