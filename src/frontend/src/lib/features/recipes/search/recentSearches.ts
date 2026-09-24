/**
 * The last few searches, on this device only.
 *
 * In `localStorage` and nowhere else, never sent to the server: what somebody
 * searched for is theirs, and a recently-searched list that followed an
 * account onto a shared tablet would be telling the household. It is a
 * convenience, so every read and write survives storage being unavailable —
 * a private window, a full disk — by simply remembering nothing.
 */
const key = 'culina.search.recent';
const kept = 5;

export function recentSearches(): string[] {
  try {
    const stored: unknown = JSON.parse(localStorage.getItem(key) ?? '[]');

    return Array.isArray(stored)
      ? stored.filter((one): one is string => typeof one === 'string').slice(0, kept)
      : [];
  } catch {
    return [];
  }
}

/** Remembers a search, most recent first, without keeping it twice. */
export function rememberSearch(query: string): string[] {
  const trimmed = query.trim();

  if (trimmed.length === 0) {
    return recentSearches();
  }

  const next = [trimmed, ...recentSearches().filter((one) => one !== trimmed)].slice(0, kept);

  try {
    localStorage.setItem(key, JSON.stringify(next));
  } catch {
    // Remembering is a courtesy; failing to is not worth telling anybody.
  }

  return next;
}
