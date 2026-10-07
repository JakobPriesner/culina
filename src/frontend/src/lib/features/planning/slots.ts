import { m } from '$shell/i18n';

import type { MealSlot } from './mealPlan.svelte';

/** The slots of a day, in the order they are shown. */
export const mealSlots = ['breakfast', 'lunch', 'dinner'] as const satisfies readonly MealSlot[];

/**
 * Written out key by key rather than looked up with a computed message key: a
 * computed key defeats tree-shaking, so every message would stay in the bundle.
 */
export const slotLabel: Record<MealSlot, () => string> = {
  breakfast: () => m['plan.slot.breakfast'](),
  lunch: () => m['plan.slot.lunch'](),
  dinner: () => m['plan.slot.dinner']()
};
