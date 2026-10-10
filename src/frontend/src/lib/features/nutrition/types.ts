/** What the nutrition endpoint says, in the app's own words; the wire shape stops at the mapper. */
export type NutritionStatus =
  'counted' | 'amountNotInGrams' | 'noAmount' | 'unknownFood' | 'excluded';

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
  readonly nameDe: string;
  readonly nameEn: string;
}

export interface NutritionLine {
  readonly ingredientId: string;
  readonly status: NutritionStatus;
  readonly food: NutritionFood | null;
  /** Grams counted, at the recipe's own yield. */
  readonly grams: number | null;
  readonly via: 'mass' | 'density' | 'eggSize' | null;
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
