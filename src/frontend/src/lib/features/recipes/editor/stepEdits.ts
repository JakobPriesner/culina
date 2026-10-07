/** Typed minutes as stored seconds; empty, unreadable or non-positive means no timer, and it caps at a day. */
export function secondsFromMinutes(written: string): number | null {
  const minutes = Number(written);

  if (written.trim() === '' || !Number.isFinite(minutes) || minutes <= 0) {
    return null;
  }

  return Math.min(86_400, Math.round(minutes * 60));
}

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
