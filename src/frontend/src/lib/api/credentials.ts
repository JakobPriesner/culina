import { csrfHeader, csrfToken } from './cookies';
import { bearerToken } from './instance';
import { native } from './platform';

/**
 * How a request proves who sends it, in one place for the typed client and the hand-built streams.
 * The website: the session cookie (`include`) plus the CSRF token on unsafe requests. The app: a bearer token and no
 * cookies at all (`omit`), which needs no CSRF token because a browser never attaches it by itself.
 */
export const credentialsMode: RequestCredentials = native ? 'omit' : 'include';

export function authorization(unsafe: boolean): Record<string, string> {
  if (native) {
    const token = bearerToken();

    return token ? { Authorization: `Bearer ${token}` } : {};
  }

  const token = unsafe ? csrfToken() : null;

  return token ? { [csrfHeader]: token } : {};
}
