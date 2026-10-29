import { resolve } from '$app/paths';

import { m } from '$shell/i18n';

import { asDate, dateToday, plusDays } from './mealPlan.svelte';

export const addDays = (from: Date, days: number) =>
  new Date(from.getFullYear(), from.getMonth(), from.getDate() + days);

/** Midday, so a time zone west of UTC cannot move a date to the day before. */
export const dayOf = (date: string) => new Date(`${date}T12:00:00`);

/** The Monday `weeks` weeks from this one. */
export const mondayOf = (weeks: number) => {
  const now = new Date();

  return asDate(addDays(now, -((now.getDay() + 6) % 7) + weeks * 7));
};

/** How many weeks from this one the week holding `date` is: 0 for this week, negative for weeks gone. */
export const weekOffsetOf = (date: string) => {
  const day = dayOf(date);
  const monday = addDays(day, -((day.getDay() + 6) % 7));

  // Rounded: a week across a clock change is an hour short or long. `|| 0` turns a rounded -0 into 0.
  return (
    Math.round((monday.getTime() - dayOf(mondayOf(0)).getTime()) / (7 * 24 * 60 * 60 * 1000)) || 0
  );
};

/** The plan opened at one day, in its week: where a list line, or a meal on the list, leads back to. */
export const planDayHref = (date: string) =>
  `${resolve('/(app)/week/plan')}?week=${weekOffsetOf(date)}&tag=${date}`;

/**
 * The days on screen as text; `formatRange` avoids repeating the month ("September 7 – 13"). A run of days that does
 * not start on a Monday says its weekdays, as it is not a week anyone knows by its dates; `short` leaves them out
 * where a phone's row has no room for them.
 */
export const rangeOf = (from: string, locale: string, count = 7, short = false) => {
  const start = dayOf(from);
  const weekdays = !short && (start.getDay() !== 1 || count !== 7);

  return new Intl.DateTimeFormat(locale, {
    weekday: weekdays ? 'short' : undefined,
    day: 'numeric',
    month: weekdays ? 'short' : 'long'
  }).formatRange(start, addDays(start, count - 1));
};

/** The week's name relative to this one; further away, the range beneath the title says which. */
export const weekTitle = (offset: number): string =>
  offset === 0
    ? m['plan.title']()
    : offset === 1
      ? m['plan.title.next']()
      : offset === -1
        ? m['plan.title.previous']()
        : m['plan.title.other']();

/** The weekday alone; Intl's combined weekday+number order varies by locale. */
export const weekdayName = (date: string, locale: string) =>
  new Intl.DateTimeFormat(locale, { weekday: 'long' }).format(dayOf(date));

/** Near days by how near they are, as a calendar says them; any other day by its weekday. */
export const dayName = (date: string, locale: string): string => {
  const today = dateToday();

  return date === today
    ? m['plan.today']()
    : date === plusDays(today, 1)
      ? m['plan.day.tomorrow']()
      : date === plusDays(today, -1)
        ? m['plan.day.yesterday']()
        : weekdayName(date, locale);
};

/** The date beside a day's name ("24. Okt."), with its weekday when the name is "Heute" or "Morgen" and so does not say it. */
export const dayDate = (date: string, locale: string): string =>
  new Intl.DateTimeFormat(locale, {
    weekday: dayName(date, locale) === weekdayName(date, locale) ? undefined : 'short',
    day: 'numeric',
    month: 'short'
  }).format(dayOf(date));

/** The coming days' name: from today, what they are; from another day, which day they start on. */
export const daysTitle = (from: string, locale: string): string =>
  from === dateToday()
    ? m['plan.days.title']()
    : m['plan.days.from']({
        day: new Intl.DateTimeFormat(locale, {
          weekday: 'short',
          day: 'numeric',
          month: 'short'
        }).format(dayOf(from))
      });
