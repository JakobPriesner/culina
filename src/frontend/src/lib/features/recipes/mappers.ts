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

/**
 * Wire shapes to the app's own, at the store boundary and nowhere else.
 *
 * This is the frontend's half of the same rule the backend follows: a contract
 * is a wire format, not a domain model. Everything awkward about the wire — a
 * nullable id that is never actually null on a read, a flattened union, a date
 * as a string — is dealt with here once.
 */
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
  // A suggestion is not a search result: nobody named an ingredient, so there
  // is nothing to report a match against.
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

/**
 * A shared recipe, which is a reading and nothing more.
 *
 * The id it is given is the token out of the link, because on this page that is
 * genuinely what identifies the recipe — there is no other name for it here,
 * and it is what the photograph is fetched under.
 */
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
  // The surface only asks whether there is a picture; where it lives is the
  // page's business, and on this page it lives under the token.
  imageId: wire.hasImage ? token : null,
  groups: wire.groups.map(toGroup),
  steps: wire.steps.map(toStep),
  tags: wire.tags,
  // Only the address travels: which library it came from and when it was
  // fetched are facts about somebody's setup, and are not on the wire here.
  sourceUrl: wire.sourceUrl ?? null,
  // A visitor cannot see when it last changed, and the surface does not draw
  // it — but the type asks, so it is the one thing here that is a placeholder.
  updatedAt: ''
});
