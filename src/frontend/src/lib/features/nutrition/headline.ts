import { everyIngredient, type Ingredient, type RecipeReading } from '$features/recipes/types';
import { formatList, formatNumber, m } from '$shell/i18n';

import { labelNumber, withBound } from './format';
import type { Nutrition, NutritionLine, NutritionReason, NutritionStatus } from './types';

/** A recipe that makes one serving: "per serving" would be the whole pot, so it says so. */
export const isWholeRecipe = (nutrition: Pick<Nutrition, 'per' | 'yield'>): boolean =>
  nutrition.per === 'serving' && nutrition.yield === 1;

/** What the figures are per, in words. */
export function perWords(nutrition: Pick<Nutrition, 'per' | 'yield'>): string {
  if (nutrition.per === 'piece') {
    return m['nutrition.perPiece']();
  }

  return isWholeRecipe(nutrition) ? m['nutrition.perRecipe']() : m['nutrition.perServing']();
}

/** The same for a column heading, where "for the whole recipe" would be too long. */
export const columnWords = (nutrition: Pick<Nutrition, 'per' | 'yield'>): string =>
  isWholeRecipe(nutrition) ? m['nutrition.perRecipeColumn']() : perWords(nutrition);

/** "520 kcal", or "at least 520 kcal" when something left out could only have added to it. */
export const energyFigure = (nutrition: Nutrition): string =>
  withBound(
    m['nutrition.energyLine']({ kcal: labelNumber('energy', nutrition.values.energyKcal) }),
    nutrition.values.energyKcal.atLeast
  );

/** The line in the recipe's meta line, short: the figure and what it is for, when it is not a single serving. */
export function metaFigure(nutrition: Nutrition): string {
  const figure = energyFigure(nutrition);

  return isWholeRecipe(nutrition) ? m['nutrition.meta.whole']({ figure }) : figure;
}

/** Null while there is no answer, or nothing in it was counted: the meta line shows no figure rather than an empty one. */
export const metaFigureOf = (nutrition: Nutrition | null): string | null =>
  nutrition && nutrition.counted > 0 ? metaFigure(nutrition) : null;

/** The ingredient lines, in the recipe's order, with what was written; a line whose ingredient was just edited away waits for the next answer. */
function writtenLines(
  nutrition: Nutrition,
  recipe: RecipeReading
): { ingredient: Ingredient; line: NutritionLine }[] {
  const written = new Map(everyIngredient(recipe).map((one) => [one.id, one] as const));

  return nutrition.ingredients.flatMap((line) => {
    const ingredient = written.get(line.ingredientId);

    return ingredient ? [{ ingredient, line }] : [];
  });
}

/** Names of the lines that could still raise the energy, as the recipe writes them. */
export const missingNames = (nutrition: Nutrition, recipe: RecipeReading): string[] =>
  nutrition.values.energyKcal.atLeast
    ? writtenLines(nutrition, recipe)
        // An implausible amount has its own hint; naming it here as well would say it twice.
        .filter(({ line }) => line.canRaiseEnergy && line.status !== 'implausible')
        .map(({ ingredient }) => ingredient.name)
    : [];

/** "without onion", "without onion and salt", "without onion, salt and 2 more"; null when nothing is missing. */
export function withoutWords(names: readonly string[]): string | null {
  switch (names.length) {
    case 0:
      return null;
    case 1:
    case 2:
      return m['nutrition.without']({ names: formatList(names) });
    default:
      return m['nutrition.withoutMore']({
        names: names.slice(0, 2).join(', '),
        count: formatNumber(names.length - 2)
      });
  }
}

/** The lines whose amount is too unlikely to count, in the recipe's order. */
export const implausibleLines = (nutrition: Nutrition, recipe: RecipeReading) =>
  writtenLines(nutrition, recipe).filter(({ line }) => line.status === 'implausible');

/** "1800 l milk": the amount as it reads on the page, then the name. */
export const amountAndName = (amount: string, name: string): string =>
  [amount, name].filter(Boolean).join(' ');

/** Why a line is not counted, in two or three words; the breakdown says it at length. Spelled out so message keys stay visible to the unused-key check. */
const shortReasons: Record<Exclude<NutritionStatus, 'counted'>, () => string> = {
  amountNotInGrams: m['nutrition.reason.amountNotInGrams'],
  noAmount: m['nutrition.reason.noAmount'],
  unknownFood: m['nutrition.reason.unknownFood'],
  excluded: m['nutrition.reason.excluded'],
  implausible: m['nutrition.short.implausible']
};

const shortUnitReasons: Record<NutritionReason, () => string> = {
  spoonOfSolid: m['nutrition.short.spoon'],
  volumeOfSolid: m['nutrition.short.volume'],
  count: m['nutrition.short.count'],
  householdUnit: m['nutrition.short.unit']
};

const shortReason = (line: NutritionLine): string =>
  line.status === 'amountNotInGrams' && line.reason
    ? shortUnitReasons[line.reason]()
    : line.status === 'counted'
      ? ''
      : shortReasons[line.status]();

/**
 * What counts of the lines written, for the editor: "7 of 9 lines count · Onion: a count · Salt: no amount".
 * At most three lines are named, in the recipe's order, then how many more; null when there is nothing to say.
 */
export function coverageWords(nutrition: Nutrition, recipe: RecipeReading): string | null {
  if (nutrition.lines === 0) {
    return null;
  }

  if (nutrition.counted === nutrition.lines) {
    return m['nutrition.coverage.all']();
  }

  const left = writtenLines(nutrition, recipe).filter(({ line }) => line.status !== 'counted');
  const named = left
    .slice(0, 3)
    .map(({ ingredient, line }) =>
      m['nutrition.coverage.line']({ name: ingredient.name, why: shortReason(line) })
    );
  const more = left.length - named.length;

  return [
    m['nutrition.coverage.some']({
      counted: formatNumber(nutrition.counted),
      lines: formatNumber(nutrition.lines)
    }),
    ...named,
    ...(more > 0 ? [m['nutrition.coverage.more']({ count: formatNumber(more) })] : [])
  ].join(' · ');
}
