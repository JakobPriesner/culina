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
  const home = resolve('/');

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
 */
export function loginUrlFor(url: URL): string {
  return `${resolve('/login')}?next=${encodeURIComponent(url.pathname + url.search)}`;
}
