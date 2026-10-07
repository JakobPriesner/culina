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
  // The wire allows an absent id (writes create lines without one); reads always have it.
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

/** The wire keeps the segment union flat; it is narrowed once here. */
const toSegment = (wire: WireSegment): StepSegment =>
  wire.type === 'ingredient'
    ? {
        kind: 'ingredient',
        ingredientId: wire.recipeIngredientId ?? '',
        name: wire.name ?? '',
        quantity: { value: wire.quantity ?? null, unit: wire.unit ?? null }
      }
    : { kind: 'text', text: wire.value ?? '' };

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

/** A just-added empty step stays on screen but out of the save, since the server refuses empty text. */
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
