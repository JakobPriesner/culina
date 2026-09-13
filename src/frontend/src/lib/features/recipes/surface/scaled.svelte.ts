import { formatQuantity, type QuantityText } from '../formatQuantity';
import { quantityLabels as labels } from '../quantityLabels';
import { factorFor, scaleQuantity, trustedFactorRange } from '../scaling';
import type { Ingredient, Recipe, Quantity } from '../types';
import { m } from '$shell/i18n';
import { preferences } from '$shell/preferences.svelte';

/**
 * One recipe at a chosen yield.
 *
 * Every amount on the surface comes from here, so the ingredient list and the
 * step text can never disagree — which is the single most common bug in recipe
 * apps and the whole reason a step stores a reference rather than the words
 * "200 g butter".
 */
export function createScaling(recipe: () => Recipe | null, target: () => number) {
  const factor = $derived.by(() => {
    const current = recipe();

    return current ? factorFor(current.yieldAmount, target()) : 1;
  });

  /**
   * Beyond these, the times and the tin stop being right.
   *
   * Culina says so rather than silently lying: a doubled cake in the same tin
   * is a raw cake, and no formula fixes that.
   */
  const timesAreDoubtful = $derived(
    factor > trustedFactorRange.highest || factor < trustedFactorRange.lowest
  );

  const show = (quantity: Quantity): QuantityText =>
    formatQuantity(scaleQuantity(quantity, factor), preferences.locale, labels);

  return {
    get factor() {
      return factor;
    },

    get timesAreDoubtful() {
      return timesAreDoubtful;
    },

    /** The base yield, worded — "4 servings" or "12 pieces". */
    get baseYieldLabel() {
      const current = recipe();

      if (!current) {
        return '';
      }

      return current.yieldKind === 'pieces'
        ? m['recipes.meta.pieces']({ count: current.yieldAmount })
        : m['recipes.meta.servings']({ count: current.yieldAmount });
    },

    /**
     * What it makes at the servings on screen.
     *
     * The base yield is what the recipe was written for; this is what the
     * amounts beside it currently say. Paper needs this one, because a printed
     * sheet has no servings control to explain itself with — and a page headed
     * "4 servings" whose ingredients are scaled to six is a quiet lie.
     */
    get currentYieldLabel() {
      const current = recipe();

      if (!current) {
        return '';
      }

      const amount = Math.round(current.yieldAmount * factor * 100) / 100;

      return current.yieldKind === 'pieces'
        ? m['recipes.meta.pieces']({ count: amount })
        : m['recipes.meta.servings']({ count: amount });
    },

    show,

    /** The same, for an ingredient. */
    amountFor: (ingredient: Ingredient): QuantityText => show(ingredient.quantity)
  };
}

export type Scaling = ReturnType<typeof createScaling>;
