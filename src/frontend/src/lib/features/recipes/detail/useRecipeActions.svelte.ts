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

interface Recipe {
  readonly recipeId: () => string;
  readonly servings: () => number;
  readonly householdId: () => string | null;
}

/** What can be done with the recipe on screen at the yield it is shown at. */
export function useRecipeActions(on: Recipe) {
  /** Replaces history so stepper taps do not bury the previous page. `goto`, not shallow `replaceState`, because the yield drives every amount on screen. */
  function scale(value: number) {
    // eslint-disable-next-line svelte/no-navigation-without-resolve -- The page's own URL, with only its yield changed.
    void goto(urlAtYield(page.url, value, recipes.detail), {
      replaceState: true,
      keepFocus: true,
      noScroll: true
    });
  }

  /** Adds the ingredients at the scaling on screen, not the recipe's own yield. */
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

  /** Copies an inherited recipe into this household and opens the copy in the editor. */
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
