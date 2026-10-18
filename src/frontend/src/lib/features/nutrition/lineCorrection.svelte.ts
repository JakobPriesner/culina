import type { AppError } from '$api';
import { unitLabel } from '$features/recipes/quantityLabels';
import { everyIngredient, type Ingredient, type RecipeReading } from '$features/recipes/types';
import { explain } from '$shell/explain';
import { m } from '$shell/i18n';
import { preferences } from '$shell/preferences.svelte';
import { toaster } from '$shell/toaster.svelte';

import { nutrition as store } from './stores/nutrition.svelte';
import type { Correction, FoodHit, Nutrition, NutritionFood, NutritionLine } from './types';
import { gramsWords } from './weights';

export interface CorrectionRow {
  readonly ingredient: Ingredient;
  readonly line: NutritionLine;
}

interface Source {
  readonly recipe: () => RecipeReading;
  readonly nutrition: () => Nutrition | null;
  /** Whose answer a correction changes; null before a household is known. */
  readonly householdId: () => string | null;
  /** How it went, said after the sheet closes; the default is a toast. */
  readonly after?: (label: string, failure: AppError | null, said: () => string) => unknown;
}

/** The answer's lines beside the ingredients they belong to, in recipe order; one edited away waits for the next answer. */
export function rowsOf(nutrition: Nutrition | null, recipe: RecipeReading): CorrectionRow[] {
  if (!nutrition) return [];

  // eslint-disable-next-line svelte/prefer-svelte-reactivity -- Built and read once per answer, never changed.
  const written = new Map(everyIngredient(recipe).map((one) => [one.id, one] as const));

  return nutrition.ingredients.flatMap((line): CorrectionRow[] => {
    const ingredient = written.get(line.ingredientId);

    return ingredient ? [{ ingredient, line }] : [];
  });
}

export const foodIn = (one: NutritionFood) =>
  preferences.locale === 'de' ? one.labelDe : one.labelEn;

const said = (label: string, failure: AppError | null, words: () => string) =>
  toaster.show(failure ? { message: () => explain(failure), tone: 'danger' } : { message: words });

/**
 * Saying what an ingredient really is, or what one unit weighs, from wherever its line is shown (the
 * recipe's breakdown, the editor's nutrition line): one way to open the sheet, one way to apply it.
 */
export function createLineCorrection(on: Source) {
  const rows = $derived(rowsOf(on.nutrition(), on.recipe()));

  let editing = $state<string | null>(null);
  /** Opened from "give the weight": the sheet goes straight to the grams. */
  let weighing = $state(false);
  // The sheet is downloaded the first time a line is corrected, not with the recipe; then it stays to fade out.
  let opened = $state(false);

  const editedRow = $derived(rows.find((row) => row.ingredient.id === editing) ?? null);

  /** The same name on other lines of this recipe changes with it; the server folds names, this only stands in until it answers. */
  const sameName = (name: string) =>
    rows
      .filter((row) => row.ingredient.name.trim().toLowerCase() === name.trim().toLowerCase())
      .map((row) => row.ingredient.id);

  /** The word for the unit of a line as the recipe writes it, so the question names what the cook sees ("clove", "tbsp"). */
  const unitWordOf = ({ ingredient, line }: CorrectionRow) =>
    line.unitKey ? unitLabel(ingredient.quantity.unit ?? line.unitKey) : null;

  /** What one unit weighs, as far as the line says: its grams at the recipe's own yield over the amount written. */
  const perUnitOf = ({ ingredient, line }: CorrectionRow) => {
    const amount = ingredient.quantity.value;

    return line.grams !== null && amount !== null && amount > 0 ? line.grams / amount : null;
  };

  const after = (label: string, failure: AppError | null, words: () => string) =>
    (on.after ?? said)(label, failure, words);

  const saidAfter = (name: string, correction: Correction) => {
    switch (correction.kind) {
      case 'food':
        return m['nutrition.correct.done']({ name, food: foodIn(correction.food) });
      case 'exclude':
        return m['nutrition.correct.doneExcluded']({ name });
      case 'default':
        // Read after the new answer: an heir that takes its choice back counts what it inherits, if anything.
        return rows.some((row) => row.ingredient.name === name && row.line.corrected)
          ? m['nutrition.correct.doneUndone']({ name })
          : m['nutrition.correct.doneDefault']({ name });
    }
  };

  /** Applies a correction: the sheet closes, the rows change at once, and a refusal puts them back and says why. */
  async function correct(correction: Correction) {
    const row = editedRow;
    const householdId = on.householdId();

    editing = null;

    if (!row || !householdId) {
      return;
    }

    const label = m['nutrition.change']({ name: row.ingredient.name });
    const { name } = row.ingredient;
    const failure = await store.correct(
      on.recipe().id,
      householdId,
      name,
      sameName(name),
      correction
    );

    await after(label, failure, () => saidAfter(name, correction));
  }

  /** Says what one unit weighs, or (null) takes it back; it counts for every line of the household with this name in this unit. */
  async function weigh(grams: number | null) {
    const row = editedRow;
    const unit = row ? unitWordOf(row) : null;
    const householdId = on.householdId();

    editing = null;

    if (!row || !householdId || !row.line.unitKey || !unit) {
      return;
    }

    const label = m['nutrition.change']({ name: row.ingredient.name });
    const { name } = row.ingredient;
    const { unitKey } = row.line;
    const ids = rows
      .filter((one) => sameName(name).includes(one.ingredient.id) && one.line.unitKey === unitKey)
      .map((one) => one.ingredient.id);
    const failure = await store.weigh(on.recipe().id, householdId, name, unitKey, ids, grams);

    await after(label, failure, () =>
      grams === null
        ? m['nutrition.weight.doneRemoved']({ name, unit })
        : m['nutrition.weight.done']({ name, unit, grams: gramsWords(grams) })
    );
  }

  return {
    get rows() {
      return rows;
    },
    get opened() {
      return opened;
    },
    get weighing() {
      return weighing;
    },
    get editedRow() {
      return editedRow;
    },
    unitWordOf,
    perUnitOf,

    /** Opens the sheet for one line; `weigh` lands on the grams for what one unit weighs. */
    edit(ingredientId: string, weigh = false) {
      editing = ingredientId;
      weighing = weigh;
      opened = true;
    },

    close() {
      editing = null;
    },

    correct,
    weigh,

    // A food from the search has no label of its own: its label is its name.
    choose: (hit: FoodHit) =>
      correct({
        kind: 'food',
        food: {
          code: hit.code,
          source: hit.source,
          nameDe: hit.nameDe,
          nameEn: hit.nameEn,
          labelDe: hit.nameDe,
          labelEn: hit.nameEn
        }
      })
  };
}

export type LineCorrection = ReturnType<typeof createLineCorrection>;
