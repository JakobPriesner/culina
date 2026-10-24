import type { DiaryDay, DiaryEntry, DiaryEntryRequest, DiaryFigures, DiaryMeal } from './types';
import { diaryMeals } from './types';

const keys = ['kcal', 'protein', 'carbohydrate', 'fat', 'saturatedFat', 'sugars', 'salt'] as const;

/** Rounds as the server stores: three decimals, so a sum here matches the one it sends. */
const stored = (value: number): number => Math.round(value * 1000) / 1000;

/**
 * A day with these entries, added up as the server adds it (Application/Users/DiaryMappings.ToDay): known values
 * summed, a lower bound when any entry is one or has no energy, an estimate when any entry is, and the typed share.
 * Used so an entry added, moved or removed shows in the totals at once, before any answer comes back.
 */
export function dayWith(date: string, entries: readonly DiaryEntry[]): DiaryDay {
  const ordered = [...entries].sort(
    (a, b) => diaryMeals.indexOf(a.meal as DiaryMeal) - diaryMeals.indexOf(b.meal as DiaryMeal)
  );
  const totals: DiaryFigures = {};

  for (const key of keys) {
    const known = ordered
      .map((entry) => entry.figures[key])
      .filter((value): value is number => typeof value === 'number');

    totals[key] = known.length === 0 ? null : stored(known.reduce((sum, value) => sum + value, 0));
  }

  return {
    date,
    entries: ordered,
    totals,
    atLeast: ordered.some((entry) => entry.atLeast || entry.figures.kcal == null),
    estimated: ordered.some((entry) => entry.estimated),
    typedKcal: stored(
      ordered
        .filter((entry) => entry.source === 'typed')
        .reduce((sum, entry) => sum + (entry.figures.kcal ?? 0), 0)
    )
  };
}

/** The request that logs the same thing again, looked up afresh: the undo of a removal, and "wie gestern". */
export function requestOf(entry: DiaryEntry, date: string, meal: string): DiaryEntryRequest {
  switch (entry.source) {
    case 'recipe':
      return {
        date,
        meal,
        recipeId: entry.recipeId ?? null,
        portions: entry.amount,
        householdId: entry.householdId ?? null
      };
    case 'food':
      return {
        date,
        meal,
        foodCode: entry.foodCode ?? null,
        amount: entry.amount,
        unit: entry.unit,
        householdId: entry.householdId ?? null
      };
    default:
      return {
        date,
        meal,
        label: entry.label,
        kcal: entry.figures.kcal ?? 0,
        protein: entry.figures.protein ?? null,
        carbohydrate: entry.figures.carbohydrate ?? null,
        fat: entry.figures.fat ?? null
      };
  }
}

/** What makes two entries "the same thing eaten" for the recent list: the recipe, the food, or the typed name. */
export function sameThing(entry: DiaryEntry): string {
  return entry.recipeId
    ? `recipe:${entry.recipeId}`
    : entry.foodCode
      ? `food:${entry.foodCode}`
      : `typed:${entry.label.trim().toLowerCase()}`;
}

/**
 * The planned meals no entry accounts for yet: each entry of a recipe confirms one planned meal of it, the one at its
 * own meal first. Leftovers planned for dinner stay a suggestion after the same dish was logged at lunch, and a portion
 * logged at another meal (after "made it", by the clock) still confirms the plan's.
 */
export function unlogged<
  Planned extends { readonly recipeId?: string | null; readonly slot: string }
>(planned: readonly Planned[], entries: readonly DiaryEntry[]): Planned[] {
  const left = entries.filter((entry) => entry.recipeId);
  const take = (meal: Planned, sameMeal: boolean): boolean => {
    const index = left.findIndex(
      (entry) => entry.recipeId === meal.recipeId && (!sameMeal || entry.meal === meal.slot)
    );

    if (index >= 0) left.splice(index, 1);

    return index >= 0;
  };

  const notAtTheirMeal = planned.filter((meal) => !take(meal, true));

  return notAtTheirMeal.filter((meal) => !take(meal, false));
}
