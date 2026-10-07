import { forgetAccountKeys } from '$shell/deviceStorage';

/** Which recipe was last started here, so leaving the create page offers it back instead of making a second empty one (a titled recipe is real at creation; there is no draft entity). */
export interface LastDraft {
  readonly recipeId: string;
  readonly title: string;
}

/** Scoped by account and household: a device or a login can hold either. */
const keyFor = (userId: string, householdId: string) => `${prefix}${userId}.${householdId}`;

const prefix = 'culina.lastDraft.';

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
    // Private browsing, disabled storage or no room: nothing to offer back.
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
    // Storage unavailable: nothing to forget.
  }
}

/** Forgets the last-started recipe for everybody but `keep`, at the same moments as the drafts (it holds a title); not on mere session expiry. */
export function forgetEveryLastDraft(keep?: string): void {
  forgetAccountKeys(prefix, keep);
}

const isLastDraft = (value: unknown): value is LastDraft =>
  typeof value === 'object' &&
  value !== null &&
  typeof (value as LastDraft).recipeId === 'string' &&
  typeof (value as LastDraft).title === 'string';
