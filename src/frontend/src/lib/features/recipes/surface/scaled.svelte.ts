import { formatQuantity, type QuantityText } from '../formatQuantity';
import { quantityLabels as labels } from '../quantityLabels';
import { factorFor, scaleQuantity, trustedFactorRange } from '../scaling';
import type { Quantity, RecipeReading } from '../types';
import { wordYield } from '../yieldWords';
import { preferences } from '$shell/preferences.svelte';

/** One recipe at a chosen yield; every amount comes from here so list and step text cannot disagree (steps store references, not words). */
export function createScaling(recipe: () => RecipeReading | null, target: () => number) {
  const factor = $derived.by(() => {
    const current = recipe();

    return current ? factorFor(current.yieldAmount, target()) : 1;
  });

  /** Beyond these the times and the tin stop being right; say so rather than silently lie. */
  const timesAreDoubtful = $derived(
    factor > trustedFactorRange.highest || factor < trustedFactorRange.lowest
  );

  const show = (quantity: Quantity): QuantityText =>
    formatQuantity(
      scaleQuantity(quantity, factor, preferences.measurementSystem),
      preferences.locale,
      labels
    );

  return {
    get factor() {
      return factor;
    },

    get timesAreDoubtful() {
      return timesAreDoubtful;
    },

    get baseYieldLabel() {
      const current = recipe();

      if (!current) {
        return '';
      }

      return wordYield(current.yieldAmount, current);
    },

    /** What it makes at the servings on screen; paper needs it since a printed sheet has no servings control. */
    get currentYieldLabel() {
      const current = recipe();

      if (!current) {
        return '';
      }

      const amount = Math.round(current.yieldAmount * factor * 100) / 100;

      return wordYield(amount, current);
    },

    show,

    amountFor: (of: { readonly quantity: Quantity }): QuantityText => show(of.quantity)
  };
}

export type Scaling = ReturnType<typeof createScaling>;
