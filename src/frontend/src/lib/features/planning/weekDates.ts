import { asDate } from './mealPlan.svelte';

/** A new Date, never an adjusted one. */
export const addDays = (from: Date, days: number) =>
  new Date(from.getFullYear(), from.getMonth(), from.getDate() + days);

/** Midday, so a time zone west of UTC cannot move a date to the day before. */
export const dayOf = (date: string) => new Date(`${date}T12:00:00`);

/**
 * The Monday `weeks` weeks from this one, as a date.
 *
 * Built by arithmetic rather than by mutating a Date: a date that is adjusted
 * in place is a date somebody else is holding a reference to.
 */
export const mondayOf = (weeks: number) => {
  const now = new Date();

  return asDate(addDays(now, -((now.getDay() + 6) % 7) + weeks * 7));
};

/**
 * The week, worded.
 *
 * `formatRange` rather than two formatted dates with a dash between them: it
 * knows not to say the month twice, and it knows which side of the number the
 * month goes on. "September 7 – 13", not "September 7 – September 13".
 */
export const rangeOf = (monday: string, locale: string) => {
  const start = dayOf(monday);

  return new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long' }).formatRange(
    start,
    addDays(start, 6)
  );
};

/**
 * The weekday on its own.
 *
 * Asking Intl for the weekday and the number at once hands back a different
 * word order per locale — and for bare "en" an order that reads oddly in a
 * column heading. The number is shown apart, stacked under it.
 */
export const weekdayName = (date: string, locale: string) =>
  new Intl.DateTimeFormat(locale, { weekday: 'long' }).format(dayOf(date));
