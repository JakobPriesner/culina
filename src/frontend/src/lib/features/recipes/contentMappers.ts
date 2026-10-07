import type { components } from '$api/generated/schema';

import type { Ingredient, IngredientGroup, Recipe, Step, StepSegment } from './types';

type WireGroup = components['schemas']['RecipesIngredientGroupContract'];
type WireIngredient = components['schemas']['RecipesIngredientContract'];
type WireStep = components['schemas']['RecipesStepContract'];
type WireSegment = components['schemas']['RecipesStepSegmentContract'];

export const toGroup = (wire: WireGroup): IngredientGroup => ({
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

export const toStep = (wire: WireStep): Step => ({
  id: wire.stepId ?? null,
  title: wire.title ?? null,
  segments: wire.segments.map(toSegment),
  uses: wire.uses ?? [],
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

/**
 * Whether a step has anything in it to store.
 *
 * "Add a step" puts an empty one on screen for the author to write in, and the
 * autosave can fire before they have. The server refuses a step with no text,
 * so until there is some it stays on screen and out of the save.
 */
const saysAnything = (step: Step): boolean =>
  step.segments.some((segment) => segment.kind === 'ingredient' || segment.text !== '');

export const toWireSteps = (steps: readonly Step[]): WireStep[] =>
  steps.filter(saysAnything).map((step) => ({
    stepId: step.id ?? undefined,
    title: step.title ?? undefined,
    durationSeconds: step.durationSeconds ?? undefined,
    uses: [...step.uses],
    segments: step.segments.map((segment) =>
      segment.kind === 'ingredient'
        ? { type: 'ingredient' as const, recipeIngredientId: segment.ingredientId }
        : { type: 'text' as const, value: segment.text }
    )
  }));

/** A recipe as the update request carries it. */
export const toWireRecipe = (recipe: Recipe) => ({
  title: recipe.title,
  description: recipe.description ?? undefined,
  language: recipe.language,
  yieldAmount: recipe.yieldAmount,
  yieldKind: recipe.yieldKind,
  yieldLabel: recipe.yieldLabel ?? undefined,
  prepMinutes: recipe.prepMinutes ?? undefined,
  cookMinutes: recipe.cookMinutes ?? undefined,
  groups: toWireGroups(recipe.groups),
  steps: toWireSteps(recipe.steps),
  tags: [...recipe.tags]
});
