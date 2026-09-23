/**
 * Whether the ranking had anything true to say about a kitchen, the last time
 * this device asked.
 *
 * The library's unchosen order depends on it — suggested once the ranking has
 * something to go on, most recent until then — and the answer only arrives
 * with the shortlist. Waiting for it put one whole round trip in front of the
 * list on every visit: sign-in check, then shortlist, then list, one after the
 * other.
 *
 * It is a fact that changes about once in the life of a kitchen, so the last
 * answer is right almost every time — the same bet the boot skeleton makes.
 * With it, the list and the shortlist are asked for together.
 *
 * Remembered per household, because a person in two kitchens can have history
 * in one and none in the other.
 */
const prefix = 'culina.ranks.';

/** Null when this device has never been told, and the page has to ask first. */
export function recallRanking(householdId: string): boolean | null {
  try {
    const value = localStorage.getItem(prefix + householdId);

    return value === null ? null : value === 'yes';
  } catch {
    // Private browsing, or a browser set to block site data. The page waits
    // for the answer, which is what it did before there was anything to recall.
    return null;
  }
}

export function rememberRanking(householdId: string, ranks: boolean): void {
  try {
    localStorage.setItem(prefix + householdId, ranks ? 'yes' : 'no');
  } catch {
    // Next visit waits for the answer again; nothing is wrong, only slower.
  }
}
