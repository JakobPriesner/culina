/**
 * Which recipe was last started here, so leaving the create page mid-thought
 * does not mean starting over.
 *
 * A recipe with just a title is real the moment it is created — there is no
 * separate draft entity to point at. What this remembers is only *which one*,
 * so the create page can offer it back rather than making a second empty
 * recipe next to the first.
 */
export interface LastDraft {
  readonly recipeId: string;
  readonly title: string;
}

/** Scoped by account and household: a device or a login can hold either. */
const keyFor = (userId: string, householdId: string) => `culina.lastDraft.${userId}.${householdId}`;

/** Never throws: forgetting to offer a draft back is not worth an error. */
export function rememberLastDraft(
  userId: string,
  householdId: string,
  recipeId: string,
  title: string
): void {
  try {
    localStorage.setItem(
      keyFor(userId, householdId),
      JSON.stringify({ recipeId, title } satisfies LastDraft)
    );
  } catch {
    // Private browsing, a disabled store, or no room left. There is simply
    // nothing to offer back next time.
  }
}

export function recallLastDraft(userId: string, householdId: string): LastDraft | null {
  try {
    const stored = localStorage.getItem(keyFor(userId, householdId));

    if (!stored) {
      return null;
    }

    const parsed: unknown = JSON.parse(stored);

    return isLastDraft(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

export function forgetLastDraft(userId: string, householdId: string): void {
  try {
    localStorage.removeItem(keyFor(userId, householdId));
  } catch {
    // Nothing to do, and nothing worth saying.
  }
}

const isLastDraft = (value: unknown): value is LastDraft =>
  typeof value === 'object' &&
  value !== null &&
  typeof (value as LastDraft).recipeId === 'string' &&
  typeof (value as LastDraft).title === 'string';
