/** What the nutrition endpoint says, in the app's own words; the wire shape stops at the mapper. */
export type NutritionStatus =
  | 'counted'
  | 'amountNotInGrams'
  | 'noAmount'
  | 'unknownFood'
  | 'unknownKind'
  | 'excluded'
  | 'implausible';

/** Why a unit is not counted as grams; only on a line whose status is `amountNotInGrams`. */
export type NutritionReason = 'spoonOfSolid' | 'volumeOfSolid' | 'count' | 'householdUnit';

export interface NutritionValue {
  /** Some ingredient weights come from typical amounts. */
  readonly estimated?: boolean;
  /** Unrounded; presentation rounds it (`rounding.ts`). */
  readonly value: number;
  /** A lower bound: something left out, or a food without this value, could only have added to it. */
  readonly atLeast: boolean;
  /** Unknown food outweighs everything counted: the bound is too far below the real value to show. */
  readonly mostlyMissing?: boolean;
}

export interface NutritionValues {
  readonly energyKj: NutritionValue;
  readonly energyKcal: NutritionValue;
  readonly fat: NutritionValue;
  readonly saturatedFat: NutritionValue;
  readonly carbohydrate: NutritionValue;
  readonly sugars: NutritionValue;
  readonly protein: NutritionValue;
  readonly salt: NutritionValue;
}

export interface NutritionFood {
  readonly code: string;
  /** Where its values come from: the table, or a package's label the household typed; absent is the table. */
  readonly source?: 'bls' | 'package';
  /** The BLS names, the citation; a household's own name for a food from a package. */
  readonly nameDe: string;
  readonly nameEn: string;
  /** What a reader calls it; the BLS name when the food has no label of its own. */
  readonly labelDe: string;
  readonly labelEn: string;
}

/** How a line's grams were reached; the last two count what the recipe does not weigh: the household's own weight, or a typical one (an estimate). */
export type NutritionVia = 'mass' | 'density' | 'eggSize' | 'householdWeight' | 'typicalWeight';

/** A usual alternative to a line's food, to choose in one tap. */
export interface NutritionVariant {
  readonly code: string;
  readonly labelDe: string;
  readonly labelEn: string;
  /** Per 100 g, so the choices can be compared. */
  readonly energyKcal: number | null;
}

export interface NutritionLine {
  readonly ingredientId: string;
  readonly status: NutritionStatus;
  readonly reason: NutritionReason | null;
  readonly food: NutritionFood | null;
  /** Grams counted, at the recipe's own yield. */
  readonly grams: number | null;
  readonly via: NutritionVia | null;
  /** A household chose this food (or to leave the line out) instead of the table's default: the reader's own, or one it inherits from. */
  readonly corrected: boolean;
  /** Which household made that choice; absent when not corrected or not known. */
  readonly chosenBy?: string | null;
  /** Which household's weight counted the line; only when `via` is `householdWeight`. */
  readonly weighedBy?: string | null;
  /** Whether leaving this line out of the figure could still make the energy higher than shown. */
  readonly canRaiseEnergy: boolean;
  /** Kilocalories per portion; the lines add up to the headline. */
  readonly energyKcal: number | null;
  /** The unit a household weight for this line is set under; absent for a mass, a volume or no amount. */
  readonly unitKey?: string | null;
  /** The citation of a typical weight; only when `via` is `typicalWeight`. */
  readonly source?: string | null;
  /** The food's usual alternatives, itself included; absent when it has none. */
  readonly variants?: readonly NutritionVariant[];
}

export interface NutritionSource {
  readonly name: string;
  readonly version: string;
  readonly publisher: string;
  readonly licence: string;
}

export interface Nutrition {
  readonly per: 'serving' | 'piece';
  /** What the recipe makes; the base the grams in the lines belong to. */
  readonly yield: number;
  readonly complete: boolean;
  readonly counted: number;
  readonly lines: number;
  readonly values: NutritionValues;
  readonly ingredients: readonly NutritionLine[];
  readonly source: NutritionSource;
}

/** A food of the table as the search offers it; the kilocalories per 100 g tell the many foods of one name apart. */
export interface FoodHit extends Pick<NutritionFood, 'code' | 'nameDe' | 'nameEn' | 'source'> {
  readonly energyKcal: number | null;
  /** Units it can be counted in besides grams, each with a cited typical weight; a table food's only. */
  readonly units?:
    readonly { readonly unit: string; readonly grams: number; readonly source: string }[] | null;
}

/** What a household can say a name is: a food, nothing to count, or whatever the table says. */
export type Correction =
  | { readonly kind: 'food'; readonly food: NutritionFood }
  | { readonly kind: 'exclude' }
  | { readonly kind: 'default' };

/** What a household said about one ingredient name: a food choice, and the weights of its units. `name` is the folded key. */
export interface IngredientFact {
  readonly name: string;
  readonly corrected: boolean;
  /** The chosen food; absent for "do not count this". */
  readonly food: NutritionFood | null;
  readonly weights: readonly { readonly unit: string; readonly grams: number }[];
}
