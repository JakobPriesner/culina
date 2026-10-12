import type { components } from '$api/generated/schema';

import type { IngredientMerge, IngredientName, LookalikeReason, MergeSuggestion } from './types';

type Schemas = components['schemas'];

export const toName = (wire: Schemas['IngredientsIngredientNameUse']): IngredientName => ({
  name: wire.name,
  recipeCount: wire.recipeCount
});

export const toMerge = (wire: Schemas['IngredientsIngredientMergeDetail']): IngredientMerge => ({
  from: wire.from,
  to: wire.to
});

export const toSuggestion = (
  wire: Schemas['IngredientsMergeSuggestionDetail']
): MergeSuggestion => ({
  from: toName(wire.from),
  to: toName(wire.to),
  reason: wire.reason as LookalikeReason
});
