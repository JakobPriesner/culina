import { presumedDiets } from '$features/recipes/stores/presumedDiets.svelte';
import { recipes } from '$features/recipes/stores/recipes.svelte';
import { m } from '$shell/i18n';
import { toaster } from '$shell/toaster.svelte';

/**
 * The diet a search only presumed this recipe keeps, and answering for it.
 *
 * Only when this household can answer: an inherited recipe is its own
 * household's to tag, so `inherited` switches the question off.
 */
export function useDietAnswer(page: { recipeId: () => string; inherited: () => boolean }) {
  const presumed = $derived(page.inherited() ? null : presumedDiets.of(page.recipeId()));
  let answering = $state(false);

  /**
   * Writes the answer as a tag — the diet's own name, or its negation, which
   * the search reads as ruling the diet out — so it is never presumed again.
   */
  async function answer(keeps: boolean) {
    const recipe = recipes.detail;

    if (!recipe || !presumed) {
      return;
    }

    const tag =
      presumed === 'vegan'
        ? keeps
          ? m['recipe.diet.tag.vegan']()
          : m['recipe.diet.tag.notVegan']()
        : keeps
          ? m['recipe.diet.tag.vegetarian']()
          : m['recipe.diet.tag.notVegetarian']();

    answering = true;

    const failure = await recipes.update({ ...recipe, tags: [...recipe.tags, tag] });

    answering = false;

    if (failure) {
      toaster.show({ message: () => m['recipe.diet.failed'](), tone: 'danger' });

      return;
    }

    presumedDiets.settle(page.recipeId());
    toaster.show({ message: () => m['recipe.diet.answered'](), tone: 'success' });
  }

  return {
    get presumed() {
      return presumed;
    },
    get answering() {
      return answering;
    },
    answer
  };
}
