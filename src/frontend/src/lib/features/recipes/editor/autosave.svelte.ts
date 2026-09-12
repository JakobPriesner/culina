import type { AppError } from '$api';

/**
 * Saves as you type, without a Save button.
 *
 * A save wall makes someone decide, halfway through writing a recipe, whether
 * they are "done" — and a recipe is never done; it is added to for years. So
 * there is no wall: the work is kept, quietly, and the only thing on screen is
 * a small word saying so.
 *
 * The debounce is long enough that a sentence is finished before it is sent and
 * short enough that closing the tab loses nothing worth missing.
 */
export type SaveState = 'idle' | 'saving' | 'saved' | 'failed';

const quietMs = 800;

export function createAutosave(save: () => Promise<AppError | null>) {
  let state = $state<SaveState>('idle');
  let failure = $state<AppError | null>(null);

  let timer: ReturnType<typeof setTimeout> | undefined;
  let inFlight = false;
  /** Something changed while a save was running, so another one is owed. */
  let owed = false;

  async function run() {
    if (inFlight) {
      owed = true;

      return;
    }

    inFlight = true;
    state = 'saving';

    const error = await save();

    inFlight = false;
    failure = error;
    state = error ? 'failed' : 'saved';

    if (owed) {
      owed = false;
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

    /** Called on every keystroke; only the last one in a pause does anything. */
    touch() {
      clearTimeout(timer);
      timer = setTimeout(() => void run(), quietMs);
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
