import { http, request, type AppError } from '$api';

/**
 * Passwords, and the ways back in when one is lost.
 *
 * Culina sends no mail, so "forgot password" cannot mean a link in an inbox.
 * It means a code the person holds: one of the ten they saved while they still
 * knew their password, or one the administrator made for them. Both are
 * entered on the same form, because the person locked out should not have to
 * know which kind they have.
 *
 * Plain functions rather than a store: none of this is state the app keeps.
 * Every code is shown once, on the screen that asked for it, and forgotten
 * with that screen.
 */

/** Sets a new password with a recovery code. Nobody is signed in afterwards. */
export async function resetPassword(
  email: string,
  code: string,
  password: string
): Promise<AppError | null> {
  const result = await request(() =>
    http.POST('/api/v1/password-resets', { body: { email, code, password } })
  );

  return result.ok ? null : result.error;
}

/** Changes the signed-in person's password. Every other device is signed out. */
export async function changePassword(
  currentPassword: string,
  newPassword: string
): Promise<AppError | null> {
  const result = await request(() =>
    http.PUT('/api/v1/users/me/password', { body: { currentPassword, newPassword } })
  );

  return result.ok ? null : result.error;
}

/** How many of the signed-in person's recovery codes are left. */
export function readRecoveryCodes() {
  return request(() => http.GET('/api/v1/users/me/recovery-codes'));
}

/** A new set of recovery codes; the old set stops working. */
export function createRecoveryCodes(password: string) {
  return request(() => http.POST('/api/v1/users/me/recovery-codes', { body: { password } }));
}

/** A one-time code for somebody else who is locked out. The administrator's only. */
export function issueRecoveryCode(email: string) {
  return request(() => http.POST('/api/v1/recovery-codes', { body: { email } }));
}
