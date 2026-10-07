/**
 * What somebody typed into a step's timer, as the seconds it stores.
 *
 * Minutes, because that is what a cook says; empty, unreadable or not above
 * zero is no timer at all, and nothing runs for longer than a day.
 */
export function secondsFromMinutes(written: string): number | null {
  const minutes = Number(written);

  if (written.trim() === '' || !Number.isFinite(minutes) || minutes <= 0) {
    return null;
  }

  return Math.min(86_400, Math.round(minutes * 60));
}

/** The list with one item moved by `by` places, or the list itself when it cannot move that far. */
export function moved<T>(items: readonly T[], index: number, by: number): readonly T[] {
  const to = index + by;

  if (to < 0 || to >= items.length) {
    return items;
  }

  const next = [...items];
  const [item] = next.splice(index, 1);

  next.splice(to, 0, item!);

  return next;
}
