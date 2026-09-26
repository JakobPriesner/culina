import { http, ok, request, type AppError, type Result } from '$api';
import type { components } from '$api/generated/schema';

/** The household an invitation leads to, and whether the caller was already in it. */
export type Redemption = components['schemas']['HouseholdsRedeemInvitationResponse'];

/** A household that sees another's recipes, and the one it inherits them from directly. */
export type Heir = components['schemas']['HouseholdsGetHeirsHeir'];

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

/**
 * Every household that sees this one's recipes: those inheriting from it, then
 * those inheriting from them.
 */
export async function heirsOf(householdId: string): Promise<Result<readonly Heir[]>> {
  const result = await request(() =>
    http.GET('/api/v1/households/{householdId}/heirs', { params: { path: { householdId } } })
  );

  return result.ok ? ok(result.value.items) : result;
}

/** Stops a household that inherits from this one directly seeing its recipes. */
export async function removeHeir(householdId: string, heirId: string): Promise<AppError | null> {
  const result = await request(() =>
    http.DELETE('/api/v1/households/{householdId}/heirs/{heirId}', {
      params: { path: { householdId, heirId } }
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
