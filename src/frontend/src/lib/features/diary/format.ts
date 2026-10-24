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

/**
 * How much of it was eaten: "1½ Portionen", "50 g", "2 Stück · ca. 220 g"; the person's own figures say so. Grams not
 * known are left out rather than written as nought.
 */
export function amountText(entry: DiaryEntry): string {
  switch (entry.source) {
    case 'recipe':
      return m['diary.portions']({ count: entry.amount, amount: portionsText(entry.amount) });
    case 'food': {
      if (entry.unit === 'g') return `${formatNumber(entry.amount)}${nbsp}g`;

      const counted = `${portionsText(entry.amount)}${nbsp}${unitLabel(entry.unit as Unit)}`;

      return entry.grams
        ? `${counted} · ${m['nutrition.about']()}${nbsp}${formatNumber(Math.round(entry.grams))}${nbsp}g`
        : counted;
    }
    default:
      return m['diary.typedByYou']();
  }
}

/** How far a figure can be trusted (docs/food-log-redesign.md §3.4, docs/glossary.md). */
export type Precision = 'exact' | 'estimated' | 'atLeast';

export const precisionOf = (flags: { atLeast: boolean; estimated: boolean }): Precision =>
  flags.atLeast ? 'atLeast' : flags.estimated ? 'estimated' : 'exact';

/** The light mark beside a figure; nothing for an exact one. */
export const precisionMark: Record<Precision, string> = { exact: '', estimated: '≈', atLeast: '≥' };

/** What a screen reader says in place of the mark, which it would read as a symbol's name. */
export function precisionWord(precision: Precision): string {
  if (precision === 'atLeast') return m['diary.mark.atLeast']();

  return precision === 'estimated' ? m['diary.mark.estimated']() : '';
}

/** Energy as a label rounds it, without a qualifier: "1.666". */
export const kcalNumber = (kcal: number, atLeast = false): string =>
  labelNumber('energy', { value: kcal, atLeast, estimated: false, mostlyMissing: false });

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

/** The day as the page's large title: "Heute", "Gestern", or its weekday. */
export function dayTitle(date: string, today: string): string {
  if (date === today) return m['diary.today']();
  if (date === addDays(today, -1)) return m['diary.yesterday']();

  return formatDate(new Date(`${date}T12:00:00`), { weekday: 'long' });
}

/** The date under that title: with its weekday when the title does not already say it. */
export function dayDateLine(date: string, today: string): string {
  const named = date === today || date === addDays(today, -1);

  return formatDate(new Date(`${date}T12:00:00`), {
    weekday: named ? 'long' : undefined,
    day: 'numeric',
    month: 'long'
  });
}

/** The Monday of the week a date is in. */
export function mondayOfDate(date: string): string {
  const day = new Date(`${date}T12:00:00`);

  return addDays(date, -((day.getDay() + 6) % 7));
}

/** A date `days` after another, both `yyyy-MM-dd`, counted in calendar days. */
export function addDays(date: string, days: number): string {
  const moved = new Date(`${date}T12:00:00`);

  moved.setDate(moved.getDate() + days);

  return `${moved.getFullYear()}-${String(moved.getMonth() + 1).padStart(2, '0')}-${String(moved.getDate()).padStart(2, '0')}`;
}
