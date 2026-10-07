import { http, request, type AppError } from '$api';
import type { components } from '$api/generated/schema';

/**
 * Creating an account and what the instance allows; shared by the sign-up form and the invitation
 * flow.
 */
export type RegistrationPolicy = components['schemas']['RegistrationGetPolicyResponse'];

export interface Registration {
  readonly email: string;
  readonly displayName: string;
  readonly password: string;
  readonly householdName?: string;
  readonly invitationCode?: string;
}

export async function readPolicy(): Promise<RegistrationPolicy> {
  const result = await request(() => http.GET('/api/v1/registration/policy'));

  // Closed by default: a sign-up form that cannot succeed is worse.
  return result.ok
    ? result.value
    : { openRegistration: false, requireInvitation: false, hasAccounts: true };
}

/**
 * Creates the account; returns the household it landed in, or `null` when the instance allows an
 * account without one.
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
