import { m } from '$shell/i18n';

import { labelNumber, withBound } from './format';
import { isWholeRecipe, withoutWords } from './headline';
import type { Nutrition } from './types';

/** One planned meal as the day sums it: what it is called, and what the server said of its recipe (null when it could not say). */
export interface PlannedFigure {
  readonly title: string;
  readonly nutrition: Nutrition | null;
}

/**
 * Whether a recipe gives a per-person figure: a portion of a recipe that makes several. A recipe made of pieces
 * says what a piece has, not what a person eats, and a recipe of one serving is the whole pot, so neither
 * can say how much a person has; they are left out of the day and named.
 */
const perPerson = (nutrition: Nutrition | null): nutrition is Nutrition =>
  nutrition !== null &&
  nutrition.per === 'serving' &&
  !isWholeRecipe(nutrition) &&
  nutrition.counted > 0;

/**
 * What one person has on a day that plans these meals, one portion of each: the energy added up, and
 * the names of the meals that could not be added. Null when no meal could (nothing to say, not "0 kcal").
 * It is a lower bound whenever a part is one or a meal is left out, since leaving something out can
 * only make the real figure higher.
 */
export function dayFigure(meals: readonly PlannedFigure[]) {
  const counted = meals.flatMap((meal) => (perPerson(meal.nutrition) ? [meal.nutrition] : []));

  if (counted.length === 0) {
    return null;
  }

  const left = [
    ...new Set(meals.filter((meal) => !perPerson(meal.nutrition)).map((meal) => meal.title))
  ];

  return {
    kcal: counted.reduce((sum, one) => sum + one.values.energyKcal.value, 0),
    atLeast: left.length > 0 || counted.some((one) => one.values.energyKcal.atLeast),
    left
  };
}

/** "at least 1,850 kcal per person · without cake"; null when nothing could be added. */
export function dayWords(meals: readonly PlannedFigure[]): string | null {
  const figure = dayFigure(meals);

  if (!figure) {
    return null;
  }

  const energy = withBound(
    m['nutrition.energyLine']({
      kcal: labelNumber('energy', { value: figure.kcal, atLeast: figure.atLeast })
    }),
    figure.atLeast
  );
  const without = withoutWords(figure.left);
  const line = m['nutrition.day.perPerson']({ figure: energy });

  return without ? `${line} · ${without}` : line;
}
