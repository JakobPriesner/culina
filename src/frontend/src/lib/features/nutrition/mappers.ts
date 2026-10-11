import type { components } from '$api/generated/schema';

import type { IngredientFact, Nutrition, NutritionLine, NutritionVia } from './types';

type Wire = components['schemas']['RecipesGetNutritionResponse'];
type WireLine = components['schemas']['RecipesGetNutritionNutritionIngredient'];

const toLine = (wire: WireLine): NutritionLine => ({
  ingredientId: wire.ingredientId,
  status: wire.status,
  reason: wire.reason ?? null,
  food: wire.food ?? null,
  grams: wire.grams ?? null,
  // The document's enum still lacks the two newest ways; the server sends them.
  via: (wire.via as NutritionVia | null | undefined) ?? null,
  corrected: wire.corrected,
  canRaiseEnergy: wire.canRaiseEnergy,
  energyKcal: wire.energyKcal ?? null,
  unitKey: wire.unitKey ?? null,
  source: wire.source ?? null,
  variants: (wire.variants ?? []).map((one) => ({ ...one, energyKcal: one.energyKcal ?? null }))
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

/** An answer this client cannot read is no answer: null, like a failed read. */
export const readNutrition = (wire: Wire): Nutrition | null => {
  try {
    return toNutrition(wire);
  } catch {
    return null;
  }
};

type WireFact = components['schemas']['NutritionIngredientFact'];

export const toFact = (wire: WireFact): IngredientFact => ({
  name: wire.name,
  corrected: wire.corrected,
  food: wire.food ?? null,
  weights: wire.weights
});
