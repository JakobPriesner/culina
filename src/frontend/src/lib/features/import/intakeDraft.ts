import type { Draft } from '$features/assistance/draftToRecipe';
import type { ParsedRecipe } from '$features/recipes/editor/parseRecipeText';
import type { Recipe } from '$features/recipes/types';

/** An unsaved baseline for the existing field comparison and draft mapper. */
export function intakeBaseline(householdId: string): Recipe {
  return {
    id: '',
    householdId,
    title: '',
    description: null,
    language: 'en',
    yieldAmount: 1,
    yieldKind: 'servings',
    yieldLabel: null,
    prepMinutes: null,
    cookMinutes: null,
    totalMinutes: null,
    imageId: null,
    groups: [],
    steps: [],
    tags: [],
    sourceUrl: null,
    createdBy: '',
    createdAt: '',
    updatedAt: '',
    version: 0
  };
}

/** Keeps a published recipe's exact quantities in the same source-review UI. */
export function intakeDraft(parsed: ParsedRecipe): Draft {
  return {
    draftId: '',
    title: parsed.title,
    yieldAmount: parsed.servings,
    yieldKind: parsed.yieldKind === 'pieces' ? 'pieces' : 'servings',
    yieldLabel: parsed.yieldLabel,
    cookMinutes: parsed.totalMinutes,
    groups: [
      {
        ingredients: parsed.ingredients.map((line) => ({
          name: line.name,
          quantity: line.quantity.value,
          unit: line.quantity.unit,
          note: line.note
        }))
      }
    ],
    steps: parsed.steps.map((text) => ({ text })),
    tags: []
  };
}
