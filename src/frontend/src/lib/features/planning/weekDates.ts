import { asDate } from './mealPlan.svelte';

export const addDays = (from: Date, days: number) =>
  new Date(from.getFullYear(), from.getMonth(), from.getDate() + days);

/** Midday, so a time zone west of UTC cannot move a date to the day before. */
export const dayOf = (date: string) => new Date(`${date}T12:00:00`);

/** The Monday `weeks` weeks from this one. */
export const mondayOf = (weeks: number) => {
  const now = new Date();

  return asDate(addDays(now, -((now.getDay() + 6) % 7) + weeks * 7));
};

/** The week as text; `formatRange` avoids repeating the month ("September 7 – 13"). */
export const rangeOf = (monday: string, locale: string) => {
  const start = dayOf(monday);

  return new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long' }).formatRange(
    start,
    addDays(start, 6)
  );
};

/** The weekday alone; Intl's combined weekday+number order varies by locale. */
export const weekdayName = (date: string, locale: string) =>
  new Intl.DateTimeFormat(locale, { weekday: 'long' }).format(dayOf(date));
