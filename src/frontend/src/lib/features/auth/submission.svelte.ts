import type { AppError } from '$api';
import { explain } from '$shell/explain';
import { createLoadingState } from '$shell/loadingState.svelte';

/**
 * The dance every form does, written once.
 *
 * Submitting, holding the failure, mapping the server's field errors onto the
 * fields by name, moving focus to the first one, and counting down a rate
 * limit. Three auth pages doing this independently would be three pages that
 * disagree about it.
 */
export interface Submission {
  /** True from the moment of submit. The button disables on this, instantly. */
  readonly inFlight: boolean;
  /**
   * True only once the delay has passed. The spinner shows on this, so a fast
   * response never flashes one.
   */
  readonly showingProgress: boolean;
  /** What went wrong overall, when it was not about one field. */
  readonly failure: AppError | null;
  /** Seconds left before a rate-limited form may be tried again. */
  readonly retryIn: number | null;
  /** The message for one field, if the server had one. */
  errorFor(field: string): string | undefined;
  /** Lets a field say where it is, so focus can be moved to it. */
  register(field: string, id: string): void;
  run(action: () => Promise<AppError | null>): Promise<boolean>;
  clear(): void;
  dispose(): void;
}

export function createSubmission(): Submission {
  let inFlight = $state(false);
  let failure = $state<AppError | null>(null);
  let retryIn = $state<number | null>(null);

  const progress = createLoadingState();

  /** Field name to control id, for moving focus. Never read reactively. */
  const fieldIds: Record<string, string> = {};

  let countdown: ReturnType<typeof setInterval> | undefined;

  function stopCountdown() {
    clearInterval(countdown);
    countdown = undefined;
  }

  /** Counts down to zero so the button can say when, not "later". */
  function startCountdown(seconds: number) {
    stopCountdown();
    retryIn = seconds;

    countdown = setInterval(() => {
      retryIn = Math.max(0, (retryIn ?? 0) - 1);

      if (retryIn === 0) {
        stopCountdown();
        retryIn = null;
      }
    }, 1000);
  }

  /** Focus goes to the first wrong field, so nobody hunts for it. */
  function focusFirstError(error: AppError) {
    const first = error.fields.find((cause) => cause.field && cause.field in fieldIds);
    const id = first?.field ? fieldIds[first.field] : undefined;

    if (id) {
      globalThis.document?.getElementById(id)?.focus();
    }
  }

  return {
    get inFlight() {
      return inFlight;
    },

    get showingProgress() {
      return progress.showing;
    },

    get failure() {
      return failure;
    },

    get retryIn() {
      return retryIn;
    },

    errorFor(field) {
      // A linear scan over at most a handful of causes, rather than a map that
      // would have to be kept in step with the failure it came from.
      const cause = failure?.fields.find((candidate) => candidate.field === field);

      return cause && explain(cause);
    },

    register(field, id) {
      fieldIds[field] = id;
    },

    async run(action) {
      if (inFlight || retryIn !== null) {
        return false;
      }

      inFlight = true;
      failure = null;
      progress.start();

      const error = await action();

      inFlight = false;
      progress.stop();

      if (!error) {
        return true;
      }

      failure = error;

      if (error.retryAfterSeconds !== null) {
        startCountdown(error.retryAfterSeconds);
      }

      focusFirstError(error);

      return false;
    },

    clear() {
      failure = null;
    },

    dispose() {
      stopCountdown();
      progress.dispose();
    }
  };
}
