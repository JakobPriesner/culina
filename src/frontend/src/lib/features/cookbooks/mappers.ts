import type { components } from '$api/generated/schema';

import type {
  Cookbook,
  CookbookDetail,
  CookbookKind,
  CookbookMembership,
  CookbookRules
} from './types';

type WireSummary = components['schemas']['CookbooksCookbookSummary'];
type WireDetail = components['schemas']['CookbooksCookbookDetail'];
type WireMembership = components['schemas']['CookbooksRecipeCookbook'];

type WireRules = components['schemas']['CookbooksCookbookRulesContract'];

const toRules = (wire: WireRules | null | undefined): CookbookRules | null =>
  wire
    ? {
        tags: wire.tags ?? [],
        ingredients: wire.ingredients ?? [],
        maxMinutes: wire.maxMinutes ?? null
      }
    : null;

export const toCookbook = (wire: WireSummary): Cookbook => ({
  id: wire.cookbookId,
  name: wire.name,
  description: wire.description ?? null,
  kind: wire.kind as CookbookKind,
  rules: toRules(wire.rules),
  recipeCount: wire.recipeCount,
  coverRecipeIds: wire.coverRecipeIds,
  updatedAt: wire.updatedAt
});

export const toDetail = (wire: WireDetail): CookbookDetail => ({
  id: wire.cookbookId,
  householdId: wire.householdId,
  name: wire.name,
  description: wire.description ?? null,
  kind: wire.kind as CookbookKind,
  rules: toRules(wire.rules),
  recipeCount: wire.recipeCount,
  coverRecipeIds: wire.coverRecipeIds,
  updatedAt: wire.updatedAt,
  version: wire.version
});

export const toMembership = (wire: WireMembership): CookbookMembership => ({
  id: wire.cookbookId,
  name: wire.name
});
