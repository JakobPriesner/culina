/**
 * Reads a cookie the browser is willing to show us.
 *
 * Only the CSRF cookie is readable — the session cookie is `HttpOnly` by
 * design — so this exists for exactly one caller and stays deliberately small.
 */
export function readCookie(name: string): string | null {
  if (typeof document === 'undefined') {
    return null;
  }

  const prefix = `${name}=`;

  for (const entry of document.cookie.split(';')) {
    const trimmed = entry.trimStart();

    if (trimmed.startsWith(prefix)) {
      return decodeURIComponent(trimmed.slice(prefix.length));
    }
  }

  return null;
}
