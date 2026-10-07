import {
  everyIngredient,
  ingredientsOf,
  type Ingredient,
  type RecipeReading,
  type Step
} from '../types';
import {
  recallIngredientsView,
  rememberIngredientsView,
  type IngredientsView
} from './ingredientsView';

/** What the panel needs to know, read afresh each time so it stays reactive. */
interface Source {
  readonly recipe: () => RecipeReading;
  readonly cooking: () => boolean;
  readonly currentStep: () => number;
}

/**
 * What the ingredient region holds, and in which of its arrangements.
 *
 * The region has two arrangements, and the switch beside its heading picks
 * between them:
 *
 * - `combined` — the whole list in one place, the same thing added up
 *   wherever the recipe asked for it. What you read before you shop.
 * - `perStep` — each step's ingredients beside that step, in the column the
 *   list would otherwise fill. What you read with a pan in your hand.
 *
 * Cooking is `perStep` taken to its conclusion — one step, and only what it
 * needs — so it neither offers the switch nor obeys it. That is also why the
 * region stays in the same place in all three: they are one arrangement at
 * three widths of attention, not three layouts.
 */
export function createIngredientPanel(source: Source) {
  let view = $state<IngredientsView>(recallIngredientsView());

  /** Every ingredient the recipe has, groups flattened, in its own order. */
  const written = $derived(everyIngredient(source.recipe()));

  const byId = $derived(ingredientsOf(source.recipe()));

  /** Whether some step asks for it, which is very nearly always the case. */
  const claimed = (ingredient: Ingredient): boolean =>
    source.recipe().steps.some((step) => step.uses.includes(ingredient.id));

  /**
   * Whether the steps have more than one list to be dealt out between.
   *
   * With one step, "by step" and "all together" are the same list — and dealing
   * it out only moves it off the panel into a second card under an empty one.
   * So a lone step is read side by side, whichever arrangement was chosen, and
   * the choice is kept for the next recipe rather than overwritten.
   */
  const divisible = $derived(source.recipe().steps.length > 1);

  /** Cooking has already contracted the region to one step; it cannot do both. */
  const perStep = $derived(view === 'perStep' && !source.cooking() && divisible);

  /**
   * What one step needs, as the ingredient lines themselves.
   *
   * A step holds ids; an id the recipe no longer has drops out rather than
   * rendering as a hole. The order is the recipe's own, because that is the
   * order the ingredient list beside it is already in.
   */
  const needsOf = (step: Step | undefined): Ingredient[] =>
    (step?.uses ?? []).map((id) => byId.get(id)).filter((one) => one !== undefined);

  /**
   * What the panel under the heading holds — three answers to three questions.
   *
   * Cooking asks "what is in my hands now", so it is the current step's list.
   * `perStep` has already put every claimed ingredient beside the step that
   * claims it, so what is left there is what no step mentions: the jar of
   * something that belongs to the whole dish, which would otherwise vanish off
   * the page altogether. Reading the combined list asks "what does this recipe
   * need", and the answer to that is all of it.
   */
  const panel = $derived.by(() => {
    if (source.cooking()) {
      return needsOf(source.recipe().steps[source.currentStep()]);
    }

    return perStep ? written.filter((one) => !claimed(one)) : written;
  });

  return {
    get view() {
      return view;
    },
    get written() {
      return written;
    },
    get divisible() {
      return divisible;
    },
    get perStep() {
      return perStep;
    },
    get panel() {
      return panel;
    },
    needsOf,

    choose(chosen: string) {
      view = chosen === 'perStep' ? 'perStep' : 'combined';
      rememberIngredientsView(view);
    }
  };
}
