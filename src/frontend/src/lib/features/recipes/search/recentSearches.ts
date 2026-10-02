import { forgetAccountKeys } from '$shell/deviceStorage';

/**
 * The last few searches, on this device only.
 *
 * In `localStorage` and nowhere else, never sent to the server: what somebody
 * searched for is theirs, and a recently-searched list that followed an
 * account onto a shared tablet would be telling the household. It is a
 * convenience, so every read and write survives storage being unavailable —
 * a private window, a full disk — by simply remembering nothing.
 *
 * Scoped by account, so it is not shown to the next person, and removed for
 * everybody else when somebody signs in or out, so it is not kept for them.
 */
const prefix = 'culina.search.';
const keyFor = (userId: string) => `${prefix}${userId}`;
const kept = 5;

export function recentSearches(userId: string): string[] {
  try {
    const stored: unknown = JSON.parse(localStorage.getItem(keyFor(userId)) ?? '[]');

    return Array.isArray(stored)
      ? stored.filter((one): one is string => typeof one === 'string').slice(0, kept)
      : [];
  } catch {
    return [];
  }
}

/** Remembers a search, most recent first, without keeping it twice. */
export function rememberSearch(userId: string, query: string): string[] {
  const trimmed = query.trim();

  if (trimmed.length === 0) {
    return recentSearches(userId);
  }

  const next = [trimmed, ...recentSearches(userId).filter((one) => one !== trimmed)].slice(0, kept);

  try {
    localStorage.setItem(keyFor(userId), JSON.stringify(next));
  } catch {
    // Remembering is a courtesy; failing to is not worth telling anybody.
  }

  return next;
}

/**
 * Forgets the recent searches of everybody but `keep`, including the one list
 * from before it was scoped by account (`culina.search.recent`).
 */
export function forgetEveryRecentSearch(keep?: string): void {
  forgetAccountKeys(prefix, keep);
}
