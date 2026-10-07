/**
 * What a typed address means, worked out client-side too (the server stays the authority): when there is enough to ask how to sign in, and the link to that server's token page.
 * Looser than a URL parser, stricter than the server: a wrong answer costs a missing link, never a connection that should have worked.
 */

/** Matches the server's ceiling. */
const maxLength = 200;

/** The scheme, host and port of what was typed, or null; a bare host counts, as https because a credential follows. */
export function originOf(typed: string): string | null {
  const text = typed.trim();

  if (text.length === 0 || text.length > maxLength) {
    return null;
  }

  const withScheme = text.includes('://') ? text : `https://${text}`;

  let url: URL;

  try {
    url = new URL(withScheme);
  } catch {
    return null;
  }

  if (url.protocol !== 'http:' && url.protocol !== 'https:') {
    return null;
  }

  // A lone word is somebody still typing, though the server accepts one ("tandoor:8080"); a link after one keystroke is noise.
  const named = url.hostname.includes('.') || url.port.length > 0;

  return named ? url.origin : null;
}

/** Where that server keeps its API tokens (Tandoor's settings page), so pasting a token starts at the right page. */
export function tokenPageOf(typed: string): string | null {
  const origin = originOf(typed);

  return origin === null ? null : `${origin}/settings`;
}
