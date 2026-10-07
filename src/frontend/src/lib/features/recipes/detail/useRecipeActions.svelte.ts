import { SvelteURL } from 'svelte/reactivity';

import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { page } from '$app/state';
import { recipes } from '$features/recipes/stores/recipes.svelte';
import { urlAtYield } from '$features/recipes/surface/yieldInUrl';
import { shopping } from '$features/shopping/stores/shopping.svelte';
import { explain } from '$shell/explain';
import { m } from '$shell/i18n';
import { toaster } from '$shell/toaster.svelte';

/** What the actions need to know about the recipe on screen. */
interface Recipe {
  readonly recipeId: () => string;
  /** The yield on screen. */
  readonly servings: () => number;
  readonly householdId: () => string | null;
}

/** What can be done with the recipe on screen at the yield it is shown at. */
export function useRecipeActions(on: Recipe) {
  /**
   * Replaced, not pushed: scaling is a view of the recipe, and every tap of the
   * stepper becoming a back-button step would bury the page you came from.
   *
   * `goto`, not `replaceState`. `replaceState` is for shallow routing — state
   * the page carries without the URL meaning anything different — so it changes
   * the address bar and tells nothing on screen that anything happened. The
   * yield is not shallow: it is what every amount on the page is derived from,
   * and a stepper that silently moved the address bar and left the amounts
   * alone is exactly the quiet wrongness this app exists to avoid.
   */
  function scale(value: number) {
    // eslint-disable-next-line svelte/no-navigation-without-resolve -- The page's own URL, with only its yield changed.
    void goto(urlAtYield(page.url, value, recipes.detail), {
      replaceState: true,
      // The thumb is still on the stepper and the eye is on the ingredient
      // list; neither should be moved by a number changing.
      keepFocus: true,
      noScroll: true
    });
  }

  /**
   * Puts the ingredients on the list at the scaling on screen.
   *
   * The scaling matters: adding a recipe you have scaled to six and getting the
   * amounts for four is the kind of quiet wrongness nobody notices until they
   * are short of butter.
   */
  async function addToShoppingList() {
    const householdId = on.householdId();

    if (!householdId) {
      return;
    }

    const failure = await shopping.addRecipe(householdId, on.recipeId(), on.servings());

    toaster.show({
      message: () => (failure ? explain(failure) : m['shopping.added']()),
      tone: failure ? 'danger' : 'success'
    });
  }

  /**
   * Makes the household on screen its own copy of an inherited recipe, and
   * opens it where it can be changed — the reason anybody asks for a copy.
   */
  async function copy() {
    const householdId = on.householdId();

    if (!householdId) {
      return;
    }

    const copied = await recipes.copy(on.recipeId(), householdId);

    if ('code' in copied) {
      toaster.show({ message: () => explain(copied), tone: 'danger' });

      return;
    }

    toaster.show({ message: () => m['recipe.copy.done'](), tone: 'success' });

    await goto(resolve('/(app)/recipes/[recipeId]/edit', { recipeId: copied.id }));
  }

  /** The yield travels with you, so cooking opens at the number you chose. */
  function startCooking() {
    const target = new SvelteURL(
      resolve('/(app)/recipes/[recipeId]/cook', { recipeId: on.recipeId() }),
      page.url
    );

    // eslint-disable-next-line svelte/no-navigation-without-resolve -- The route is resolved before its yield is appended.
    void goto(urlAtYield(target, on.servings(), recipes.detail));
  }

  return { scale, addToShoppingList, copy, startCooking };
}
