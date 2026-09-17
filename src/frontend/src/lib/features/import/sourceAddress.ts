/**
 * What a typed address means, worked out on this side too.
 *
 * The server is the authority — it normalises the address again, and it is the
 * one that refuses a bad one. This exists for the two things that have to
 * happen before the form is submitted: deciding when there is enough to ask
 * how to sign in, and building the link to that server's own token page.
 *
 * Deliberately a little more forgiving than a URL parser and a little less than
 * the server: it answers "does this look like a server yet", not "is this
 * allowed". Getting that wrong here costs a link that does not appear, never a
 * connection that should have worked.
 */

/** Longer than any host anyone types, matching the server's own ceiling. */
const maxLength = 200;

/**
 * The scheme, host and port of what was typed, or null.
 *
 * A bare host counts, because that is what most people paste — and https when
 * they did not say, because whatever follows carries a credential.
 */
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

  // A lone word is somebody still typing, not a server. The server accepts one
  // — "tandoor:8080" is a real address on a real home network — but offering a
  // link to `https://t/settings` after the first keystroke is noise.
  const named = url.hostname.includes('.') || url.port.length > 0;

  return named ? url.origin : null;
}

/**
 * Where that server keeps its API tokens.
 *
 * Tandoor's own settings page. Sending somebody to the page they would make a
 * token on is most of the work of "paste a token here" — the other way round,
 * they have to know the feature exists, know what it is called, and find it.
 */
export function tokenPageOf(typed: string): string | null {
  const origin = originOf(typed);

  return origin === null ? null : `${origin}/settings`;
}
