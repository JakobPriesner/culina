import { http, request, type AppError } from '$api';

/**
 * The ways back in that come by mail, offered only where the instance sends mail. Asking always answers the same
 * whether or not the address has an account, so the pages say "if it has an account" and never more.
 */

/** Mails a link that signs in without a password. */
export async function requestSignInLink(email: string): Promise<AppError | null> {
  const result = await request(() => http.POST('/api/v1/sign-in-links', { body: { email } }));

  return result.ok ? null : result.error;
}

/** Mails a link (and a code) that sets a new password. */
export async function requestPasswordResetLink(email: string): Promise<AppError | null> {
  const result = await request(() =>
    http.POST('/api/v1/password-reset-links', { body: { email } })
  );

  return result.ok ? null : result.error;
}

/**
 * The values a mailed link carries after `#` (`#token=…`, `#email=…&code=…`). Read from the fragment because a
 * fragment never reaches a server, proxy or Referer; dropped from the address bar once read, so a secret does not
 * stay in history or get shared with a copied URL.
 */
export function takeLinkFragment(): URLSearchParams {
  const hash = globalThis.location?.hash ?? '';
  const values = new URLSearchParams(hash.startsWith('#') ? hash.slice(1) : hash);

  if (hash.length > 0) {
    globalThis.history?.replaceState(
      globalThis.history.state,
      '',
      `${location.pathname}${location.search}`
    );
  }

  return values;
}
