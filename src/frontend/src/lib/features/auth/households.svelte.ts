import { http, ok, request, type AppError, type Result } from '$api';
import type { components } from '$api/generated/schema';

/** The household an invitation leads to, and whether the caller was already in it. */
export type Redemption = components['schemas']['HouseholdsRedeemInvitationResponse'];

/** A household that sees another's recipes, and the one it inherits them from directly. */
export type Heir = components['schemas']['HouseholdsGetHeirsHeir'];

/** The ways out of having no household and into another; one made while in a household may inherit from it in the same request, so none exists unaware of what it should see. */
export async function createHousehold(
  name: string,
  inheritsFrom: string | null = null
): Promise<string | AppError> {
  const result = await request(() =>
    http.POST('/api/v1/households', { body: { name, inheritsFrom } })
  );

  return result.ok ? result.value.householdId : result.error;
}

/** Chooses whose recipes a household sees besides its own, or none; the session is re-read after, since only the server knows the chain. */
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

/** Every household that sees this one's recipes, direct inheritors first. */
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

export type DeletedHousehold = components['schemas']['HouseholdsHouseholdSummary'];

/** Puts a household in the bin (owners only; the server decides too). Reads it first: memberships carry no version, and a fresh read proves the caller still sees it. */
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

export type Invitation = components['schemas']['HouseholdsGetInvitationByCodeResponse'];

/** Names the household a code admits to without using the code up; signed in only, since a link is a bearer token and should not reveal whose kitchen it opens early. */
export function readInvitation(code: string): Promise<Result<Invitation>> {
  return request(() => http.GET('/api/v1/invitations/{code}', { params: { path: { code } } }));
}

export async function redeemInvitation(code: string): Promise<Redemption | AppError> {
  const result = await request(() =>
    http.POST('/api/v1/invitations/{code}/redemptions', { params: { path: { code } } })
  );

  return result.ok ? result.value : result.error;
}
