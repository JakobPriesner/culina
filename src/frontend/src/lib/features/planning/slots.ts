import { m } from '$shell/i18n';

import type { MealSlot } from './mealPlan.svelte';

export const mealSlots = ['breakfast', 'lunch', 'dinner'] as const satisfies readonly MealSlot[];

/** Written out key by key: a computed message key defeats tree-shaking. */
export const slotLabel: Record<MealSlot, () => string> = {
  breakfast: () => m['plan.slot.breakfast'](),
  lunch: () => m['plan.slot.lunch'](),
  dinner: () => m['plan.slot.dinner']()
};
