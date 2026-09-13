import type { Recipe } from '../types';

/**
 * What was typed, kept on this device until the server has it.
 *
 * Autosave is quick but it is not instantaneous, and the gap is where work goes
 * missing: the tab closed mid-sentence, the session that expired while somebody
 * was thinking, the kitchen with no signal, the language switched from the
 * header. None of those is unusual, and losing a paragraph to any of them is
 * the kind of thing that stops people writing recipes down.
 *
 * So every change is written here first, synchronously, and removed only once
 * the server has answered. It is a journal, not a sync engine: there is no
 * queue, no replay and no conflict resolution. What it promises is that nothing
 * disappears silently — the text comes back, and the app says plainly that it
 * has not been saved yet.
 */
export interface JournalEntry {
  readonly recipe: Recipe;
  /** When it was written here, so the app can say how old it is. */
  readonly at: string;
}

/**
 * Scoped by account as well as recipe.
 *
 * A device is shared. Somebody else's half-written recipe is not yours to be
 * shown, and a key that names only the recipe would show it.
 */
const keyFor = (userId: string, recipeId: string) => `culina.draft.${userId}.${recipeId}`;

const prefix = 'culina.draft.';

/** Writes the draft. Never throws: a full or blocked store is not worth an error. */
export function remember(userId: string, recipeId: string, recipe: Recipe): void {
  try {
    localStorage.setItem(
      keyFor(userId, recipeId),
      JSON.stringify({ recipe, at: new Date().toISOString() } satisfies JournalEntry)
    );
  } catch {
    // Private browsing, a disabled store, or no room left. The editor still
    // works; it just cannot promise to survive a reload, and it does not claim
    // to — the indicator only says the work is kept here when this succeeded.
  }
}

/** Reads the draft back, or null when there is none to read. */
export function recall(userId: string, recipeId: string): JournalEntry | null {
  try {
    const stored = localStorage.getItem(keyFor(userId, recipeId));

    if (!stored) {
      return null;
    }

    const parsed: unknown = JSON.parse(stored);

    return isEntry(parsed) ? parsed : null;
  } catch {
    // A half-written or hand-edited value is not a draft. Losing it is the
    // right outcome; throwing on the way into an editor is not.
    return null;
  }
}

export function forget(userId: string, recipeId: string): void {
  try {
    localStorage.removeItem(keyFor(userId, recipeId));
  } catch {
    // Nothing to do, and nothing worth saying.
  }
}

/**
 * Removes every draft on this device.
 *
 * Called when a session ends. An unsent recipe belongs to whoever wrote it, and
 * leaving it on a shared tablet is exactly the leak the account-scoped key was
 * meant to prevent — the key stops it being *shown*, not stored.
 */
export function forgetEveryDraft(): void {
  try {
    for (const key of Object.keys(localStorage)) {
      if (key.startsWith(prefix)) {
        localStorage.removeItem(key);
      }
    }
  } catch {
    // As above.
  }
}

const isEntry = (value: unknown): value is JournalEntry =>
  typeof value === 'object' &&
  value !== null &&
  'recipe' in value &&
  typeof (value as JournalEntry).recipe === 'object' &&
  (value as JournalEntry).recipe !== null &&
  'at' in value;
