import { http, request, type AppError } from '$api';
import type { components } from '$api/generated/schema';

/** The household an invitation leads to, and whether the caller was already in it. */
export type Redemption = components['schemas']['HouseholdsRedeemInvitationResponse'];

/**
 * The two ways out of having no household.
 *
 * Kept together because they are alternatives to each other: the welcome screen
 * offers both, and neither means anything without the other.
 */
export async function createHousehold(name: string): Promise<string | AppError> {
  const result = await request(() => http.POST('/api/v1/households', { body: { name } }));

  return result.ok ? result.value.householdId : result.error;
}

export async function redeemInvitation(code: string): Promise<Redemption | AppError> {
  const result = await request(() =>
    http.POST('/api/v1/invitations/{code}/redemptions', { params: { path: { code } } })
  );

  return result.ok ? result.value : result.error;
}
