import { forgetAccountKeys } from '$shell/deviceStorage';

/**
 * The last few searches, in `localStorage` only and never sent to the server (they must not follow an account onto a shared tablet).
 * Every read and write survives unavailable storage by remembering nothing; scoped by account, and removed for everybody else on sign-in or out.
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

/** Forgets everybody's recent searches but `keep`'s, including the unscoped legacy list (`culina.search.recent`). */
export function forgetEveryRecentSearch(keep?: string): void {
  forgetAccountKeys(prefix, keep);
}
