import type { ParsedRecipe } from '$features/recipes/editor/parseRecipeText';
import type { Recipe } from '$features/recipes/types';

/** Fields a pasted recipe adds to the new one. Yield (with what it counts and its word) and time only when parsed; steps stay plain text (no guessed @ingredient links). */
export const recipePatchFromPaste = (pasted: ParsedRecipe): Partial<Recipe> => ({
  ...(pasted.servings === undefined
    ? {}
    : {
        yieldAmount: pasted.servings,
        yieldKind: pasted.yieldKind ?? 'servings',
        yieldLabel: pasted.yieldLabel ?? null
      }),
  ...(pasted.totalMinutes === undefined ? {} : { cookMinutes: pasted.totalMinutes }),
  groups: [
    {
      id: null,
      name: null,
      ingredients: pasted.ingredients.map((one) => ({
        id: '',
        quantity: one.quantity,
        name: one.name,
        note: one.note
      }))
    }
  ],
  steps: pasted.steps.map((text) => ({
    id: null,
    title: null,
    segments: [{ kind: 'text' as const, text }],
    uses: [],
    durationSeconds: null
  }))
});
