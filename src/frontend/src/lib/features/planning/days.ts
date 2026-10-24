/** Calendar days as the API spells them, and the Monday weeks the server plans, reads and shops in. */

/** The date as the API spells it (YYYY-MM-DD). */
export const asDate = (day: Date): string =>
  `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`;

/** The Monday of the week `date` belongs to: the server plans, reads and shops in Monday weeks. */
export const weekStart = (date: string): string =>
  plusDays(date, -((new Date(`${date}T12:00:00`).getDay() + 6) % 7));

/** `date` moved by `days`, at midday so a clock change cannot slip it a day. */
export function plusDays(date: string, days: number): string {
  const day = new Date(`${date}T12:00:00`);

  return asDate(new Date(day.getFullYear(), day.getMonth(), day.getDate() + days, 12));
}

/** The Mondays of every week the `count` days from `from` touch: a span from Saturday to Tuesday is two weeks. */
export const weeksOf = (from: string, count: number): string[] => {
  const weeks: string[] = [];

  for (
    let monday = weekStart(from);
    monday <= plusDays(from, count - 1);
    monday = plusDays(monday, 7)
  ) {
    weeks.push(monday);
  }

  return weeks;
};
