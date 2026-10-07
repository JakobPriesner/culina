import { resolve } from '$app/paths';

/**
 * Where to go after signing in. The value comes from the URL, so it is hostile: only a path within this app is accepted
 * (not `https://x`, `//x`, `/\x`, `/%09/x` or relative paths), found by resolving it as the browser would and requiring this origin.
 * The resolved path is returned, never the raw string, so no consumer can read it differently.
 */
export function safeRedirect(next: string | null): string {
  const home = resolve('/(app)');

  if (!next || !next.startsWith('/')) {
    return home;
  }

  let target: URL;
  try {
    target = new URL(next, location.origin);
  } catch {
    // `//exa mple` and the like: a host the parser refuses is no place to go.
    return home;
  }

  if (target.origin !== location.origin) {
    return home;
  }

  return target.pathname + target.search + target.hash;
}

/** The sign-in URL that remembers where someone was going; one definition for the route guard and the expired-session handler. The reason lets the page say why. */
export function loginUrlFor(url: URL, reason?: 'expired'): string {
  const login = resolve('/(auth)/login');
  // Already on the sign-in page: pointing `next` at itself would nest one level deeper per sign-in.
  const target =
    url.pathname === login ? safeRedirect(url.searchParams.get('next')) : url.pathname + url.search;
  const next = `?next=${encodeURIComponent(target)}`;

  return `${login}${next}${reason ? `&reason=${reason}` : ''}`;
}
