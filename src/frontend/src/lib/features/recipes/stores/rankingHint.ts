/**
 * Whether ranking had anything to say about a kitchen, last time this device asked. It decides the library's
 * unchosen order but only arrives with the shortlist; the last answer rarely changes, so list and shortlist
 * load together. Per household: one kitchen can have history and another none.
 */
const prefix = 'culina.ranks.';

/** Null when this device has never been told, and the page has to ask first. */
export function recallRanking(householdId: string): boolean | null {
  try {
    const value = localStorage.getItem(prefix + householdId);

    return value === null ? null : value === 'yes';
  } catch {
    // Private browsing or blocked site data: the page waits for the answer, as before.
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
