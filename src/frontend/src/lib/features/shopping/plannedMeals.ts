import { mealSlots } from '$features/planning/slots';

import type { ShoppingItem } from './stores/shopping.svelte';

/** What one planned meal asks of the list: a line still to buy, and how much of it this meal accounts for. */
export interface MealLine {
  readonly itemId: string;
  readonly name: string;
  readonly quantity: number | null | undefined;
  readonly unit: string | null | undefined;
}

export interface ListedMeal {
  readonly recipeId: string;
  readonly recipeTitle: string;
  readonly slot: string | null | undefined;
  readonly lines: readonly MealLine[];
}

export interface ListedDay {
  readonly date: string;
  /** Before today: the shop it was for has probably happened, yet its lines are still here. */
  readonly isPast: boolean;
  readonly meals: readonly ListedMeal[];
}

/** Position in the day; a slot this client does not know goes last. */
const slotRank = (slot: string | null | undefined) => {
  const rank = mealSlots.findIndex((known) => known === slot);

  return rank === -1 ? mealSlots.length : rank;
};

/**
 * The planned meals behind the lines still to buy, by day: what is on the list because of which meal.
 * Lines ticked off are left out, since they are no longer asked of the shop. A source added without a day belongs to no meal.
 * `today` is a `YYYY-MM-DD` date, so days compare as text.
 */
export function plannedMealsOf(items: readonly ShoppingItem[], today: string): ListedDay[] {
  const meals = new Map<string, { date: string; meal: ListedMeal & { lines: MealLine[] } }>();

  for (const item of items) {
    if (item.isChecked) {
      continue;
    }

    for (const source of item.sources ?? []) {
      if (!source.plannedDate) {
        continue;
      }

      const key = `${source.plannedDate}|${source.plannedSlot ?? ''}|${source.recipeId}`;
      let entry = meals.get(key);

      if (!entry) {
        entry = {
          date: source.plannedDate,
          meal: {
            recipeId: source.recipeId,
            recipeTitle: source.recipeTitle,
            slot: source.plannedSlot,
            lines: []
          }
        };
        meals.set(key, entry);
      }

      entry.meal.lines.push({
        itemId: item.itemId,
        name: item.name,
        quantity: source.quantity,
        unit: source.unit
      });
    }
  }

  const days = new Map<string, ListedMeal[]>();

  for (const { date, meal } of meals.values()) {
    days.set(date, [...(days.get(date) ?? []), meal]);
  }

  return [...days.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([date, dayMeals]) => ({
      date,
      isPast: date < today,
      meals: dayMeals.sort(
        (a, b) => slotRank(a.slot) - slotRank(b.slot) || a.recipeTitle.localeCompare(b.recipeTitle)
      )
    }));
}
