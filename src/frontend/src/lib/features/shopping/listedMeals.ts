import { mealSlots } from '$features/planning/slots';

import type { ShoppingItem } from './stores/shopping.svelte';

/** What one meal (or recipe) asks of the list: a line, how much of it this meal accounts for, and whether it is bought. */
export interface MealLine {
  readonly itemId: string;
  readonly name: string;
  readonly quantity: number | null | undefined;
  readonly unit: string | null | undefined;
  readonly checked: boolean;
}

export interface ListedMeal {
  /** The planned meal, for taking its lines back off; null for a recipe added by itself, or a list cached before the API said. */
  readonly planEntryId: string | null;
  readonly recipeId: string;
  readonly recipeTitle: string;
  readonly slot: string | null | undefined;
  readonly lines: readonly MealLine[];
  /** How many of its lines are already in the trolley. */
  readonly done: number;
}

export interface ListedDay {
  /** The meal's day; null for recipes added by themselves (from their page, a cookbook, the list). */
  readonly date: string | null;
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
 * The meals and recipes behind the lines, by day: what is on the list because of which meal, so any of
 * them can be traced and taken back off. Lines already bought stay with their meal, counted as done,
 * so ticking shows progress rather than making lines vanish. Recipes added by themselves come last.
 * `today` is a `YYYY-MM-DD` date, so days compare as text.
 */
export function listedMealsOf(items: readonly ShoppingItem[], today: string): ListedDay[] {
  type Building = { date: string | null; meal: Omit<ListedMeal, 'done'> & { lines: MealLine[] } };
  const meals = new Map<string, Building>();

  for (const item of items) {
    for (const source of item.sources ?? []) {
      const date = source.plannedDate ?? null;
      const key = date
        ? `${date}|${source.plannedSlot ?? ''}|${source.recipeId}`
        : `|alone|${source.recipeId}`;
      let entry = meals.get(key);

      if (!entry) {
        entry = {
          date,
          meal: {
            planEntryId: source.planEntryId ?? null,
            recipeId: source.recipeId,
            recipeTitle: source.recipeTitle,
            slot: date ? source.plannedSlot : null,
            lines: []
          }
        };
        meals.set(key, entry);
      }

      // A recipe added by itself twice asks twice for one line: one entry, the amounts summed.
      const same = entry.meal.lines.findIndex((line) => line.itemId === item.itemId);

      if (same >= 0) {
        const earlier = entry.meal.lines[same]!;

        entry.meal.lines[same] = {
          ...earlier,
          quantity:
            earlier.quantity != null && source.quantity != null
              ? earlier.quantity + source.quantity
              : (earlier.quantity ?? source.quantity)
        };
      } else {
        entry.meal.lines.push({
          itemId: item.itemId,
          name: item.name,
          quantity: source.quantity,
          unit: source.unit,
          checked: item.isChecked
        });
      }
    }
  }

  const days = new Map<string | null, ListedMeal[]>();

  for (const { date, meal } of meals.values()) {
    const done = meal.lines.filter((line) => line.checked).length;

    // A meal whose every line is bought has nothing left to trace or take back.
    if (done === meal.lines.length) continue;

    days.set(date, [...(days.get(date) ?? []), { ...meal, done }]);
  }

  return [...days.entries()]
    .sort(([a], [b]) => (a === null ? 1 : b === null ? -1 : a.localeCompare(b)))
    .map(([date, dayMeals]) => ({
      date,
      isPast: date !== null && date < today,
      meals: dayMeals.sort(
        (a, b) => slotRank(a.slot) - slotRank(b.slot) || a.recipeTitle.localeCompare(b.recipeTitle)
      )
    }));
}
