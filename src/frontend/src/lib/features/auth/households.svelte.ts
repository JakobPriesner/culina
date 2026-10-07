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

/** A household in the bin that the caller owns, and could bring back. */
export type DeletedHousehold = components['schemas']['HouseholdsHouseholdSummary'];

/**
 * Puts a household in the bin. Owners only — the server says so too, and is
 * the one that decides.
 *
 * Read immediately before deleting rather than trusting a version the page
 * loaded earlier: the session's memberships carry none, and a household
 * somebody renamed a minute ago should be deleted as it is now, not refused
 * for a change nobody can see. The read is also what proves the caller can
 * still see it at all.
 */
export async function deleteHousehold(householdId: string): Promise<AppError | null> {
  const current = await request(() =>
    http.GET('/api/v1/households/{householdId}', { params: { path: { householdId } } })
  );

  if (!current.ok) {
    return current.error;
  }

  const result = await request(() =>
    http.DELETE('/api/v1/households/{householdId}', {
      params: { path: { householdId } },
      headers: { 'If-Match': `"v${current.value.version}"` }
    })
  );

  return result.ok ? null : result.error;
}

/** The deleted households this person owns, newest first. */
export async function deletedHouseholds(): Promise<Result<readonly DeletedHousehold[]>> {
  const result = await request(() =>
    http.GET('/api/v1/households', { params: { query: { deleted: 'true' } } })
  );

  return result.ok ? ok(result.value.items) : result;
}

/** Takes a household out of the bin, with its members, recipes and cookbooks. Owners only. */
export async function restoreHousehold(householdId: string): Promise<AppError | null> {
  const result = await request(() =>
    http.POST('/api/v1/households/{householdId}/restorations', {
      params: { path: { householdId } }
    })
  );

  return result.ok ? null : result.error;
}

/** Whose household an invitation is for, read before anybody decides to join it. */
export type Invitation = components['schemas']['HouseholdsGetInvitationByCodeResponse'];

/**
 * Names the household a code admits to, without using the code up. Signed in
 * only: somebody signed out has nothing to decide yet, and a link is a bearer
 * token whose holder should not learn whose kitchen it opens before then.
 */
export function readInvitation(code: string): Promise<Result<Invitation>> {
  return request(() => http.GET('/api/v1/invitations/{code}', { params: { path: { code } } }));
}

export async function redeemInvitation(code: string): Promise<Redemption | AppError> {
  const result = await request(() =>
    http.POST('/api/v1/invitations/{code}/redemptions', { params: { path: { code } } })
  );

  return result.ok ? result.value : result.error;
}
