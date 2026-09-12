import type { components } from '$api/generated/schema';

import type {
  Ingredient,
  IngredientGroup,
  Recipe,
  RecipeSummary,
  Step,
  StepSegment
} from './types';

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
type WireGroup = components['schemas']['RecipesIngredientGroupContract'];
type WireIngredient = components['schemas']['RecipesIngredientContract'];
type WireStep = components['schemas']['RecipesStepContract'];
type WireSegment = components['schemas']['RecipesStepSegmentContract'];

export const toSummary = (wire: WireSummary): RecipeSummary => ({
  id: wire.recipeId,
  title: wire.title,
  imageId: wire.imageId ?? null,
  totalMinutes: wire.totalMinutes ?? null,
  yieldAmount: wire.yieldAmount,
  yieldKind: wire.yieldKind,
  tags: wire.tags,
  cookCount: wire.cookCount,
  updatedAt: wire.updatedAt,
  match: wire.ingredientMatch
    ? {
        matched: wire.ingredientMatch.matched,
        requested: wire.ingredientMatch.requested,
        missing: wire.ingredientMatch.missing
      }
    : null
});

export const toRecipe = (wire: WireRecipe): Recipe => ({
  id: wire.recipeId,
  householdId: wire.householdId,
  title: wire.title,
  description: wire.description ?? null,
  language: wire.language,
  yieldAmount: wire.yieldAmount,
  yieldKind: wire.yieldKind,
  prepMinutes: wire.prepMinutes ?? null,
  cookMinutes: wire.cookMinutes ?? null,
  totalMinutes: wire.totalMinutes ?? null,
  imageId: wire.imageId ?? null,
  groups: wire.groups.map(toGroup),
  steps: wire.steps.map(toStep),
  tags: wire.tags,
  createdBy: wire.createdBy,
  createdAt: wire.createdAt,
  updatedAt: wire.updatedAt,
  version: wire.version
});

const toGroup = (wire: WireGroup): IngredientGroup => ({
  id: wire.groupId ?? null,
  name: wire.name ?? null,
  ingredients: wire.ingredients.map(toIngredient)
});

const toIngredient = (wire: WireIngredient): Ingredient => ({
  // The wire allows an absent id because a *write* creates lines without one.
  // On a read the server has always assigned one.
  id: wire.ingredientId ?? '',
  quantity: { value: wire.quantity ?? null, unit: wire.unit ?? null },
  name: wire.name,
  note: wire.note ?? null
});

const toStep = (wire: WireStep): Step => ({
  id: wire.stepId ?? null,
  segments: wire.segments.map(toSegment),
  durationSeconds: wire.durationSeconds ?? null
});

/**
 * Narrows the flattened union the contract carries.
 *
 * The wire keeps it flat on purpose, so a generated client does not have to
 * narrow one; the narrowing happens here, once, and components get a real
 * discriminated union.
 */
const toSegment = (wire: WireSegment): StepSegment =>
  wire.type === 'ingredient'
    ? {
        kind: 'ingredient',
        ingredientId: wire.recipeIngredientId ?? '',
        name: wire.name ?? '',
        quantity: { value: wire.quantity ?? null, unit: wire.unit ?? null }
      }
    : { kind: 'text', text: wire.value ?? '' };

/** The app's shape back onto the wire, for a write. */
export const toWireGroups = (groups: readonly IngredientGroup[]): WireGroup[] =>
  groups.map((group) => ({
    groupId: group.id ?? undefined,
    name: group.name ?? undefined,
    ingredients: group.ingredients.map((one) => ({
      ingredientId: one.id || undefined,
      quantity: one.quantity.value ?? undefined,
      unit: one.quantity.unit ?? undefined,
      name: one.name,
      note: one.note ?? undefined
    }))
  }));

export const toWireSteps = (steps: readonly Step[]): WireStep[] =>
  steps.map((step) => ({
    stepId: step.id ?? undefined,
    durationSeconds: step.durationSeconds ?? undefined,
    segments: step.segments.map((segment) =>
      segment.kind === 'ingredient'
        ? { type: 'ingredient' as const, recipeIngredientId: segment.ingredientId }
        : { type: 'text' as const, value: segment.text }
    )
  }));
