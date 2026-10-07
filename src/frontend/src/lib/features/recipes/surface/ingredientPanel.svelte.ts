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

interface Source {
  readonly recipe: () => RecipeReading;
  readonly cooking: () => boolean;
  readonly currentStep: () => number;
}

/**
 * The ingredient region: `combined` (whole list) or `perStep` (each step's ingredients beside it).
 * Cooking shows only the current step's list, so it neither offers nor obeys the switch.
 */
export function createIngredientPanel(source: Source) {
  let view = $state<IngredientsView>(recallIngredientsView());

  const written = $derived(everyIngredient(source.recipe()));

  const byId = $derived(ingredientsOf(source.recipe()));

  const claimed = (ingredient: Ingredient): boolean =>
    source.recipe().steps.some((step) => step.uses.includes(ingredient.id));

  /** A lone step makes "by step" and "combined" the same list, so it is never split (the stored choice is kept). */
  const divisible = $derived(source.recipe().steps.length > 1);

  const perStep = $derived(view === 'perStep' && !source.cooking() && divisible);

  /** A step's ingredient lines; ids the recipe no longer has are dropped. */
  const needsOf = (step: Step | undefined): Ingredient[] =>
    (step?.uses ?? []).map((id) => byId.get(id)).filter((one) => one !== undefined);

  /** Cooking: the current step's list. `perStep`: only ingredients no step claims. Otherwise all of them. */
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
