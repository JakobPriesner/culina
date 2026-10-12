/** A name the household's recipes use, and how many of them. */
export interface IngredientName {
  readonly name: string;
  readonly recipeCount: number;
}

/** A standing merge: lines written `from` are saved as `to`. */
export interface IngredientMerge {
  readonly from: string;
  readonly to: string;
}

/** Why two names look like one ingredient. */
export type LookalikeReason = 'spelling' | 'same_food' | 'typo';

/** A proposal to merge `from` into the more used `to`. */
export interface MergeSuggestion {
  readonly from: IngredientName;
  readonly to: IngredientName;
  readonly reason: LookalikeReason;
}
