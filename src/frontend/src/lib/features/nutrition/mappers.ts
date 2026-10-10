import type { components } from '$api/generated/schema';

import type { Nutrition, NutritionLine } from './types';

type Wire = components['schemas']['RecipesGetNutritionResponse'];
type WireLine = components['schemas']['RecipesGetNutritionNutritionIngredient'];

const toLine = (wire: WireLine): NutritionLine => ({
  ingredientId: wire.ingredientId,
  status: wire.status,
  reason: wire.reason ?? null,
  food: wire.food ?? null,
  grams: wire.grams ?? null,
  via: wire.via ?? null,
  corrected: wire.corrected,
  canRaiseEnergy: wire.canRaiseEnergy,
  energyKcal: wire.energyKcal ?? null
});

export const toNutrition = (wire: Wire): Nutrition => ({
  per: wire.per,
  yield: wire.yield,
  complete: wire.complete,
  counted: wire.counted,
  lines: wire.lines,
  values: wire.values,
  ingredients: wire.ingredients.map(toLine),
  source: wire.source
});
