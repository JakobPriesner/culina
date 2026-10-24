import { formatDate, formatNumber, m } from '$shell/i18n';
import { labelNumber, nbsp, qualified } from '$features/nutrition/format';
import { unitLabel } from '$features/recipes/quantityLabels';
import type { Unit } from '$features/recipes/units';

import type { DiaryEntry, DiaryMeal } from './types';

const quarters = new Map<number, string>([
  [0.25, '¼'],
  [0.5, '½'],
  [0.75, '¾']
]);

/** Portions as a cook says them: "1", "½", "1½"; anything finer as a number. */
export function portionsText(portions: number): string {
  const whole = Math.floor(portions);
  const glyph = quarters.get(Number((portions - whole).toFixed(2)));

  if (portions === whole) {
    return formatNumber(portions);
  }

  return glyph
    ? `${whole > 0 ? whole : ''}${glyph}`
    : formatNumber(portions, { maximumFractionDigits: 2 });
}

/** How much of it was eaten: "1½ Portionen", "50 g", "2 Stück · ca. 220 g"; nothing for the person's own figures. */
export function amountText(entry: DiaryEntry): string {
  switch (entry.source) {
    case 'recipe':
      return m['diary.portions']({ count: entry.amount, amount: portionsText(entry.amount) });
    case 'food':
      return entry.unit === 'g'
        ? `${formatNumber(entry.amount)}${nbsp}g`
        : `${portionsText(entry.amount)}${nbsp}${unitLabel(entry.unit as Unit)} · ${m['nutrition.about']()}${nbsp}${formatNumber(Math.round(entry.grams ?? 0))}${nbsp}g`;
    default:
      return m['diary.typedByYou']();
  }
}

/** Energy as a label prints it, with "mind." for a lower bound and "ca." for an estimate. */
export function kcalText(kcal: number, flags: { atLeast: boolean; estimated: boolean }): string {
  return qualified(
    `${labelNumber('energy', { value: kcal, atLeast: flags.atLeast, estimated: flags.estimated, mostlyMissing: false })}${nbsp}kcal`,
    flags
  );
}

/** Grams of a macro as a label prints them: "64 g", "7,4 g". */
export function gramsText(grams: number, atLeast = false): string {
  return `${labelNumber('macro', { value: grams, atLeast, estimated: false, mostlyMissing: false })}${nbsp}g`;
}

/** The meal's name. Written out key by key: a computed message key defeats tree-shaking. */
export const mealLabel: Record<DiaryMeal, () => string> = {
  breakfast: () => m['plan.slot.breakfast'](),
  lunch: () => m['plan.slot.lunch'](),
  dinner: () => m['plan.slot.dinner'](),
  snack: () => m['diary.snack']()
};

/** "Heute", "Gestern", "Morgen", or the weekday and date. */
export function dayLabel(date: string, today: string): string {
  const difference = Math.round((Date.parse(date) - Date.parse(today)) / 86_400_000);

  if (difference === 0) return m['diary.today']();
  if (difference === -1) return m['diary.yesterday']();
  if (difference === 1) return m['diary.tomorrow']();

  return formatDate(new Date(`${date}T12:00:00`), {
    weekday: 'long',
    day: 'numeric',
    month: 'long'
  });
}

/** A date `days` after another, both `yyyy-MM-dd`, counted in calendar days. */
export function addDays(date: string, days: number): string {
  const moved = new Date(`${date}T12:00:00`);

  moved.setDate(moved.getDate() + days);

  return `${moved.getFullYear()}-${String(moved.getMonth() + 1).padStart(2, '0')}-${String(moved.getDate()).padStart(2, '0')}`;
}
