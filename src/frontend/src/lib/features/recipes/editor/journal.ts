import { forgetAccountKeys } from '$shell/deviceStorage';

import type { Recipe } from '../types';

/**
 * Drafts kept on this device until the server has answered; a journal, not a sync engine (no queue,
 * replay or conflict resolution).
 */
export interface JournalEntry {
  readonly recipe: Recipe;
  readonly at: string;
}

/** Scoped by account as well as recipe, so a shared device never shows someone else's draft. */
const keyFor = (userId: string, recipeId: string) => `culina.draft.${userId}.${recipeId}`;

const prefix = 'culina.draft.';

/** Writes the draft; never throws, since a full or blocked store isn't worth an error. */
export function remember(userId: string, recipeId: string, recipe: Recipe): void {
  try {
    localStorage.setItem(
      keyFor(userId, recipeId),
      JSON.stringify({ recipe, at: new Date().toISOString() } satisfies JournalEntry)
    );
  } catch {
    // Private browsing or no room: the editor works, it just doesn't claim the work is kept here.
  }
}

export function recall(userId: string, recipeId: string): JournalEntry | null {
  try {
    const stored = localStorage.getItem(keyFor(userId, recipeId));

    if (!stored) {
      return null;
    }

    const parsed: unknown = JSON.parse(stored);

    return isEntry(parsed) ? parsed : null;
  } catch {
    // A half-written or hand-edited value is not a draft; throwing on the way into an editor is
    // worse.
    return null;
  }
}

export function forget(userId: string, recipeId: string): void {
  try {
    localStorage.removeItem(keyFor(userId, recipeId));
  } catch {
    // Nothing to do.
  }
}

/**
 * Removes every draft except `keep`'s, on sign-out and sign-in; not on session expiry, when the
 * journal is needed. The key only stops drafts being shown, not stored.
 */
export function forgetEveryDraft(keep?: string): void {
  forgetAccountKeys(prefix, keep);
}

const isEntry = (value: unknown): value is JournalEntry =>
  typeof value === 'object' &&
  value !== null &&
  'recipe' in value &&
  typeof (value as JournalEntry).recipe === 'object' &&
  (value as JournalEntry).recipe !== null &&
  'at' in value;
