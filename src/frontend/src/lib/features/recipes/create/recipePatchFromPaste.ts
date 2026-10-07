import type { ParsedRecipe } from '$features/recipes/editor/parseRecipeText';
import type { Recipe } from '$features/recipes/types';

/**
 * What a pasted recipe adds to the one that was just created for it.
 *
 * Only a site that published structured data knows the yield and the time. A
 * pasted block of words does not say, and the recipe keeps its defaults.
 *
 * The steps are plain text for now: the words are what was pasted, and an
 * ingredient is mentioned in a step by typing @ — guessing which ones were
 * meant is the silent linking this editor deliberately stopped doing.
 */
export const recipePatchFromPaste = (pasted: ParsedRecipe): Partial<Recipe> => ({
  ...(pasted.servings === undefined ? {} : { yieldAmount: pasted.servings }),
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
