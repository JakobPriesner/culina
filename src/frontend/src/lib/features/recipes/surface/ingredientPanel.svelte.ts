import {
  everyIngredient,
  ingredientsOf,
  type Ingredient,
  type RecipeReading,
  type Step
} from '../types';
import { inferUses } from './inferredUses';
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

/** While cooking: the step's own list, or all of them to prep ahead. Not remembered: every cook starts on the step. */
export type CookingView = 'step' | 'all';

/**
 * The ingredient region: `combined` (whole list) or `perStep` (each step's ingredients beside it).
 * Cooking has its own switch: the current step's list, or all of them.
 */
export function createIngredientPanel(source: Source) {
  let view = $state<IngredientsView>(recallIngredientsView());
  let cookingView = $state<CookingView>('step');

  const written = $derived(everyIngredient(source.recipe()));

  const byId = $derived(ingredientsOf(source.recipe()));

  /** Guessed from each step's words; used only for a step its author linked nothing to, so a half-linked recipe still has every step. */
  const inferred = $derived(inferUses(source.recipe().steps, written));

  const usesOf = (step: Step): readonly string[] =>
    step.uses.length > 0 ? step.uses : (inferred[source.recipe().steps.indexOf(step)] ?? []);

  const claimed = (ingredient: Ingredient): boolean =>
    source.recipe().steps.some((step) => usesOf(step).includes(ingredient.id));

  /** A lone step makes "by step" and "combined" the same list, so it is never split (the stored choice is kept). */
  const divisible = $derived(source.recipe().steps.length > 1);

  const perStep = $derived(view === 'perStep' && !source.cooking() && divisible);

  const needsOf = (step: Step | undefined): Ingredient[] =>
    (step ? usesOf(step) : []).map((id) => byId.get(id)).filter((one) => one !== undefined);

  /**
   * Cooking: the current step's list unless all were asked for, or all of them when a lone step is the whole method.
   * `perStep`: only ingredients no step claims. Otherwise all of them.
   */
  const panel = $derived.by(() => {
    if (source.cooking() && divisible && cookingView === 'step') {
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
    get cookingView() {
      return cookingView;
    },
    get panel() {
      return panel;
    },
    needsOf,

    choose(chosen: string) {
      view = chosen === 'perStep' ? 'perStep' : 'combined';
      rememberIngredientsView(view);
    },

    chooseWhileCooking(chosen: string) {
      cookingView = chosen === 'all' ? 'all' : 'step';
    }
  };
}
