/** What the nutrition endpoint says, in the app's own words; the wire shape stops at the mapper. */
export type NutritionStatus =
  'counted' | 'amountNotInGrams' | 'noAmount' | 'unknownFood' | 'excluded' | 'implausible';

/** Why a unit is not counted as grams; only on a line whose status is `amountNotInGrams`. */
export type NutritionReason = 'spoonOfSolid' | 'volumeOfSolid' | 'count' | 'householdUnit';

export interface NutritionValue {
  /** Unrounded; presentation rounds it (`rounding.ts`). */
  readonly value: number;
  /** A lower bound: something left out, or a food without this value, could only have added to it. */
  readonly atLeast: boolean;
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
  /** The BLS names, the citation. */
  readonly nameDe: string;
  readonly nameEn: string;
  /** What a reader calls it; the BLS name when the food has no label of its own. */
  readonly labelDe: string;
  readonly labelEn: string;
}

export interface NutritionLine {
  readonly ingredientId: string;
  readonly status: NutritionStatus;
  readonly reason: NutritionReason | null;
  readonly food: NutritionFood | null;
  /** Grams counted, at the recipe's own yield. */
  readonly grams: number | null;
  readonly via: 'mass' | 'density' | 'eggSize' | null;
  /** The household chose this food (or to leave the line out) instead of the table's default. */
  readonly corrected: boolean;
  /** Whether leaving this line out of the figure could still make the energy higher than shown. */
  readonly canRaiseEnergy: boolean;
  /** Kilocalories per portion; the lines add up to the headline. */
  readonly energyKcal: number | null;
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
export interface FoodHit extends Pick<NutritionFood, 'code' | 'nameDe' | 'nameEn'> {
  readonly energyKcal: number | null;
}

/** What a household can say a name is: a food, nothing to count, or whatever the table says. */
export type Correction =
  | { readonly kind: 'food'; readonly food: NutritionFood }
  | { readonly kind: 'exclude' }
  | { readonly kind: 'default' };
