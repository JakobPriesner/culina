import { http, request, type AppError } from '$api';
import type { components } from '$api/generated/schema';

/** The household an invitation leads to, and whether the caller was already in it. */
export type Redemption = components['schemas']['HouseholdsRedeemInvitationResponse'];

/**
 * The two ways out of having no household, and the way into another one.
 *
 * Kept together because they are alternatives to each other: the welcome screen
 * offers both, and neither means anything without the other.
 *
 * A household made while already in one may inherit that one's recipes from
 * the start — asked in the same request, so there is never a household that
 * exists but has not yet been told what it should see.
 */
export async function createHousehold(
  name: string,
  inheritsFrom: string | null = null
): Promise<string | AppError> {
  const result = await request(() =>
    http.POST('/api/v1/households', { body: { name, inheritsFrom } })
  );

  return result.ok ? result.value.householdId : result.error;
}

/**
 * Chooses whose recipes a household sees besides its own, or none.
 *
 * The answer is not kept: the session is read again afterwards, because the
 * chain it carries names every household up the line and only the server
 * knows those.
 */
export async function setInheritance(
  householdId: string,
  parentId: string | null
): Promise<AppError | null> {
  const result = await request(() =>
    http.PUT('/api/v1/households/{householdId}/inheritance', {
      params: { path: { householdId } },
      body: { householdId: parentId }
    })
  );

  return result.ok ? null : result.error;
}

export async function redeemInvitation(code: string): Promise<Redemption | AppError> {
  const result = await request(() =>
    http.POST('/api/v1/invitations/{code}/redemptions', { params: { path: { code } } })
  );

  return result.ok ? result.value : result.error;
}
