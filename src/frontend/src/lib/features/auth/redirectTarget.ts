import { resolve } from '$app/paths';

/**
 * Where to go after signing in.
 *
 * The value comes from the URL, which means it comes from whoever wrote the
 * link — so it is treated as hostile. Only a path within this app is accepted:
 * anything else is an open redirect, which is how a convincing sign-in page
 * ends up handing people to somewhere that is not Culina.
 *
 * Rejected, with the reason each one matters:
 *
 * - `https://evil.example` — another site outright.
 * - `//evil.example` — protocol-relative, and a browser reads it as another
 *   site even though it starts with a slash.
 * - `/\evil.example` — some parsers normalise the backslash to a slash, which
 *   turns it into the case above.
 * - anything not starting with `/` — relative to wherever we happen to be.
 */
export function safeRedirect(next: string | null): string {
  const home = resolve('/(app)');

  if (!next || !next.startsWith('/') || next.startsWith('//') || next.startsWith('/\\')) {
    return home;
  }

  return next;
}

/**
 * The sign-in URL that remembers where someone was going.
 *
 * One definition, used by the route guard and by the handler for a session that
 * expired mid-use, so the two cannot drift into sending people to different
 * places.
 *
 * The reason is what lets the sign-in page say why somebody is suddenly
 * looking at it. Without one, a session ending mid-use is a bounce nobody
 * explained.
 */
export function loginUrlFor(url: URL, reason?: 'expired'): string {
  const login = resolve('/(auth)/login');
  // Already on the sign-in page, the place to go is the one it already has.
  // Pointing `next` at the sign-in page itself would land people back on the
  // form after signing in, and nest one level deeper each time.
  const target =
    url.pathname === login ? safeRedirect(url.searchParams.get('next')) : url.pathname + url.search;
  const next = `?next=${encodeURIComponent(target)}`;

  return `${login}${next}${reason ? `&reason=${reason}` : ''}`;
}
