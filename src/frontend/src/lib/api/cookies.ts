/** Readable on purpose: echoing it in a header is what an attacker's page cannot do. */
const csrfCookie = 'culina.csrf';

/** The CSRF token as the browser holds it right now, which another tab can change. */
export function csrfToken(): string | null {
  return readCookie(csrfCookie);
}

/** Reads a cookie the browser shows us: only the CSRF one is readable (the session cookie is `HttpOnly`), so this has one caller. */
function readCookie(name: string): string | null {
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
