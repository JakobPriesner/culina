import { http, request, type AppError } from '$api';
import type { components } from '$api/generated/schema';

/**
 * Creating an account, and what the instance will allow.
 *
 * Kept out of the page so the sign-up form and the invitation flow ask the
 * instance the same question and read the answer the same way.
 */
export type RegistrationPolicy = components['schemas']['RegistrationGetPolicyResponse'];

export interface Registration {
  readonly email: string;
  readonly displayName: string;
  readonly password: string;
  readonly householdName?: string;
  readonly invitationCode?: string;
}

/** What the instance allows, or a sensible closed default if it will not say. */
export async function readPolicy(): Promise<RegistrationPolicy> {
  const result = await request(() => http.GET('/api/v1/registration/policy'));

  // Closed by default. Offering a sign-up form because a request failed would
  // send people into a form that cannot succeed.
  return result.ok
    ? result.value
    : { openRegistration: false, requireInvitation: false, hasAccounts: true };
}

/**
 * Creates the account.
 *
 * Returns the household it landed in, which is `null` when the instance allows
 * an account without one — the caller then offers a way to get one rather than
 * leaving a dead end.
 */
export async function register(
  details: Registration
): Promise<{ householdId: string | null } | AppError> {
  const result = await request(() =>
    http.POST('/api/v1/users', {
      body: {
        email: details.email,
        displayName: details.displayName,
        password: details.password,
        householdName: details.householdName || undefined,
        invitationCode: details.invitationCode || undefined
      }
    })
  );

  return result.ok ? { householdId: result.value.householdId ?? null } : result.error;
}
