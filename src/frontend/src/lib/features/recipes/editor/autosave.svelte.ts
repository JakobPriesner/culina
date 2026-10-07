import type { AppError } from '$api';

/** Saves as you type, with no Save button (a recipe is never "done"); the debounce lets a sentence finish yet loses nothing worth missing on tab close. */
export type SaveState = 'idle' | 'saving' | 'saved' | 'failed';

const quietMs = 800;

export function createAutosave(save: () => Promise<AppError | null>) {
  let state = $state<SaveState>('idle');
  let failure = $state<AppError | null>(null);

  let timer: ReturnType<typeof setTimeout> | undefined;
  let inFlight = false;
  /** Edits made and how many the server has: saving after an untouched visit would bump the version under other editors and write back an unread note. */
  let edits = 0;
  let savedEdits = 0;

  async function run() {
    // A save already running is followed by another below if typing went on.
    if (inFlight || edits === savedEdits) {
      return;
    }

    inFlight = true;
    state = 'saving';

    const sending = edits;
    const error = await save();

    inFlight = false;
    failure = error;
    state = error ? 'failed' : 'saved';

    if (!error) {
      savedEdits = sending;
    }

    // Typing went on during that save, so another is owed.
    if (edits !== sending) {
      await run();
    }
  }

  return {
    get state() {
      return state;
    },

    get failure() {
      return failure;
    },

    /** Called on every keystroke; only the last one in a pause acts. */
    touch() {
      edits += 1;
      clearTimeout(timer);
      timer = setTimeout(() => void run(), quietMs);
    },

    /** Forgets the last failure, for a conflict resolved by taking the other version (the message would describe a situation that no longer exists). */
    clear() {
      clearTimeout(timer);
      savedEdits = edits;
      failure = null;
      state = 'idle';
    },

    /** Sends whatever is owed right now — on blur, or on leaving the page. */
    async flush() {
      clearTimeout(timer);
      await run();
    },

    dispose() {
      clearTimeout(timer);
    }
  };
}

export type Autosave = ReturnType<typeof createAutosave>;
