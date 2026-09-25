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
  /**
   * What this step is called, or null to be called by its number.
   *
   * Null on nearly every step. A recipe with a base, a filling and a glaze is
   * the one that wants names, and there "Step 2" is the least useful thing that
   * could be written above the sentence.
   */
  readonly title: string | null;
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
  /** The recipe's own word for what it makes, or null for the usual one. */
  readonly yieldLabel: string | null;
  readonly tags: readonly string[];
  /** How many times this person has made it. */
  readonly cookCount: number;
  /** When this person last made it, or null. What "not since April" is written from. */
  readonly lastCookedAt: string | null;
  readonly updatedAt: string;
  readonly match: IngredientMatch | null;
  /**
   * Why it answers a search its title does not name. Absent outside a search,
   * and null for a title match.
   */
  readonly matchReason?: MatchReason | null;
}

/**
 * Why a recipe is in a search it does not name in its title.
 *
 * `concept` is a match through what the recipe is rather than what it says —
 * Waffeln for "Nachtisch" — and always carries a reason, so an associative
 * match never looks like a real one.
 */
export interface MatchReason {
  readonly kind: 'ingredient' | 'tag' | 'text' | 'concept';
  /** The ingredient or tag as the recipe writes it, or the concept in its language. */
  readonly term: string | null;
}

export type ChipKind = 'time' | 'quick' | 'diet' | 'meal' | 'cuisine' | 'ingredient' | 'exclusion';

/**
 * One thing the server read a query to mean, and the characters it read it
 * from: deleting `start`–`end` from the query and asking again removes it.
 */
export interface SearchChip {
  readonly kind: ChipKind;
  /** Minutes, a stable key (`vegetarian`, `dinner`, `italian`), or a word as typed. */
  readonly value: string;
  readonly text: string;
  readonly start: number;
  readonly end: number;
  /** For an ingredient or an exclusion, the thing itself as typed. */
  readonly word: string | null;
}

/** A query, as the server understood it. */
export interface Interpretation {
  readonly freeText: string;
  readonly chips: readonly SearchChip[];
  /** What was typed, when the words were corrected; `freeText` is the correction. */
  readonly correctedFrom: string | null;
  /** Readings set aside because nothing matched all of them. */
  readonly relaxed: readonly SearchChip[];
  /** Two readings that rule each other out, when that is why nothing matched. */
  readonly conflict: readonly SearchChip[];
}

export interface Facet {
  readonly value: string;
  readonly label: string | null;
  readonly count: number;
}

/** Refinements that split the results, counted over all of them. */
export interface Facets {
  readonly tags: readonly Facet[];
  readonly times: readonly Facet[];
  readonly cuisines: readonly Facet[];
}

/** What a half-typed search could become. */
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

/**
 * Why a recipe was suggested.
 *
 * Null whenever no single signal decided the ranking, and that is an ordinary
 * answer meaning show nothing. The server sends a code and at most a subject,
 * never a sentence: the wording is the client's because the client is what
 * knows which of two languages the person reads.
 */
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
  /** The tag, ingredient or person it is about, when it is about something nameable. */
  readonly subject: string | null;
}

/** One suggested recipe, and why. */
export interface Suggestion extends RecipeSummary {
  readonly reason: SuggestionReason | null;
}

/**
 * Why a recipe is shown beside another: what both of them are, or what both
 * are made from — at most three things, worded by the server in the language
 * of the recipe being read.
 */
export interface RelatedReason {
  readonly kind: 'kinds' | 'ingredients';
  readonly shared: readonly string[];
}

/** A recipe like the one being read, and why. */
export interface RelatedRecipe extends RecipeSummary {
  readonly reason: RelatedReason;
}

/** How well a recipe fits what you said you have. */
export interface IngredientMatch {
  readonly matched: number;
  readonly requested: number;
  readonly missing: number;
}

/**
 * A recipe as it is read.
 *
 * Everything the page draws and nothing about whose kitchen it is. The split
 * exists because a link hands a stranger exactly this much — so the surface
 * asks for exactly this much, and the fields a visitor must never see cannot be
 * reached from the one component that renders both.
 */
export interface RecipeReading {
  readonly id: string;
  readonly title: string;
  readonly description: string | null;
  readonly language: RecipeLanguage;
  readonly yieldAmount: number;
  readonly yieldKind: YieldKind;
  /** The recipe's own word for what it makes, or null for the usual one. */
  readonly yieldLabel: string | null;
  readonly prepMinutes: number | null;
  readonly cookMinutes: number | null;
  readonly totalMinutes: number | null;
  readonly imageId: string | null;
  readonly groups: readonly IngredientGroup[];
  readonly steps: readonly Step[];
  readonly tags: readonly string[];
  /**
   * Where it was originally published, when it was not written here.
   *
   * Null for most recipes, and the whole of what the app keeps about an import:
   * an imported recipe is an ordinary recipe in every other respect — edited,
   * cooked, scaled and planned like one somebody typed — and one credit under
   * the title is all that ever said otherwise.
   */
  readonly sourceUrl: string | null;
  readonly updatedAt: string;
}

/** The same recipe, to the household that keeps it. */
export interface Recipe extends RecipeReading {
  readonly householdId: string;
  readonly createdBy: string;
  readonly createdAt: string;
  /** Sent back as `If-Match` on a write, so two editors cannot clobber. */
  readonly version: number;
}

/**
 * Every ingredient in the recipe, in the order it was written.
 *
 * Flat, because the groups are a detail of how it was typed: what the surface
 * shows is one list, or one list per step.
 */
export const everyIngredient = (recipe: RecipeReading): readonly Ingredient[] =>
  recipe.groups.flatMap((group) => group.ingredients);

/** The same, for lookups by id. */
export function ingredientsOf(recipe: RecipeReading): Map<string, Ingredient> {
  return new Map(everyIngredient(recipe).map((one) => [one.id, one] as const));
}
