import type { components } from '$api/generated/schema';

import type {
  Recipe,
  RecipeReading,
  RecipeSummary,
  RelatedRecipe,
  Suggestion,
  SuggestionReasonCode,
  YieldKind
} from './types';
import { toGroup, toStep } from './contentMappers';
import { toReason } from './searchMappers';

export { toWireGroups, toWireRecipe, toWireSteps } from './contentMappers';
export { toCompletion, toFacets, toInterpretation } from './searchMappers';

/** Wire shapes to the app's own, at the store boundary only; wire awkwardness (nullable ids, flattened unions, string dates) is dealt with once here. */
type WireSummary = components['schemas']['RecipesGetAllRecipeSummary'];
type WireRecipe = components['schemas']['RecipesRecipeDetail'];
type WireSuggestion = components['schemas']['SuggestionsGetAllSuggestion'];
type WireRelated = components['schemas']['RecipesGetRelatedRelatedRecipe'];
type WireShared = components['schemas']['RecipesGetSharedResponse'];

export const toSummary = (wire: WireSummary): RecipeSummary => ({
  id: wire.recipeId,
  householdId: wire.householdId,
  title: wire.title,
  imageId: wire.imageId ?? null,
  totalMinutes: wire.totalMinutes ?? null,
  yieldAmount: wire.yieldAmount,
  yieldKind: wire.yieldKind as YieldKind,
  yieldLabel: wire.yieldLabel ?? null,
  tags: wire.tags,
  cookCount: wire.cookCount,
  lastCookedAt: wire.lastCookedAt ?? null,
  updatedAt: wire.updatedAt,
  match: wire.ingredientMatch
    ? {
        matched: wire.ingredientMatch.matched,
        requested: wire.ingredientMatch.requested,
        missing: wire.ingredientMatch.missing
      }
    : null,
  matchReason: wire.matchReason ? toReason(wire.matchReason) : null,
  presumedDiet:
    wire.presumedDiet === 'vegetarian' || wire.presumedDiet === 'vegan' ? wire.presumedDiet : null
});

export const toSuggestion = (wire: WireSuggestion): Suggestion => ({
  id: wire.recipeId,
  householdId: wire.householdId,
  title: wire.title,
  imageId: wire.imageId ?? null,
  totalMinutes: wire.totalMinutes ?? null,
  yieldAmount: wire.yieldAmount,
  yieldKind: wire.yieldKind as YieldKind,
  yieldLabel: wire.yieldLabel ?? null,
  tags: wire.tags,
  cookCount: wire.cookCount,
  lastCookedAt: wire.lastCookedAt ?? null,
  updatedAt: wire.updatedAt,
  // A suggestion names no ingredient, so there is no match to report.
  match: null,
  reason: wire.reason
    ? { code: wire.reason.code as SuggestionReasonCode, subject: wire.reason.subject ?? null }
    : null
});

export const toRelated = (wire: WireRelated): RelatedRecipe => ({
  id: wire.recipeId,
  householdId: wire.householdId,
  title: wire.title,
  imageId: wire.imageId ?? null,
  totalMinutes: wire.totalMinutes ?? null,
  yieldAmount: wire.yieldAmount,
  yieldKind: wire.yieldKind as YieldKind,
  yieldLabel: wire.yieldLabel ?? null,
  tags: wire.tags,
  cookCount: wire.cookCount,
  lastCookedAt: wire.lastCookedAt ?? null,
  updatedAt: wire.updatedAt,
  match: null,
  reason: {
    kind: wire.reason.kind === 'kinds' ? 'kinds' : 'ingredients',
    shared: wire.reason.shared
  }
});

export const toRecipe = (wire: WireRecipe): Recipe => ({
  id: wire.recipeId,
  householdId: wire.householdId,
  title: wire.title,
  description: wire.description ?? null,
  language: wire.language,
  yieldAmount: wire.yieldAmount,
  yieldKind: wire.yieldKind,
  yieldLabel: wire.yieldLabel ?? null,
  prepMinutes: wire.prepMinutes ?? null,
  cookMinutes: wire.cookMinutes ?? null,
  totalMinutes: wire.totalMinutes ?? null,
  imageId: wire.imageId ?? null,
  groups: wire.groups.map(toGroup),
  steps: wire.steps.map(toStep),
  tags: wire.tags,
  sourceUrl: wire.origin?.sourceUrl ?? null,
  createdBy: wire.createdBy,
  createdAt: wire.createdAt,
  updatedAt: wire.updatedAt,
  version: wire.version
});

/** A shared recipe, a reading only; its id is the link token, which is what identifies it (and its photo) on that page. */
export const toSharedRecipe = (token: string, wire: WireShared): RecipeReading => ({
  id: token,
  title: wire.title,
  description: wire.description ?? null,
  language: wire.language,
  yieldAmount: wire.yieldAmount,
  yieldKind: wire.yieldKind,
  yieldLabel: wire.yieldLabel ?? null,
  prepMinutes: wire.prepMinutes ?? null,
  cookMinutes: wire.cookMinutes ?? null,
  totalMinutes: wire.totalMinutes ?? null,
  // The surface only asks whether there is a picture; on this page it lives under the token.
  imageId: wire.hasImage ? token : null,
  groups: wire.groups.map(toGroup),
  steps: wire.steps.map(toStep),
  tags: wire.tags,
  // Only the address travels: the source library and fetch time are setup facts, not on the wire here.
  sourceUrl: wire.sourceUrl ?? null,
  // A visitor cannot see when it last changed and the surface does not draw it, but the type asks: a placeholder.
  updatedAt: ''
});
