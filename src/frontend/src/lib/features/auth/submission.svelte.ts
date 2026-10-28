import type { AppError } from '$api';
import { explain } from '$shell/explain';
import { createLoadingState } from '$shell/loadingState.svelte';

/** The shared form dance: submit, hold the failure, map field errors onto fields, focus the first, count down rate limits. */
export interface Submission {
  /** True from the moment of submit. The button disables on this, instantly. */
  readonly inFlight: boolean;
  /** True only once the delay has passed, so a fast response never flashes the spinner. */
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

  /** Focus goes to the first wrong field in page order (the server lists causes in its own order), so nobody hunts for it. */
  function focusFirstError(error: AppError, startedOn: Element | null) {
    const document = globalThis.document;
    const active = document?.activeElement;

    // Somebody who moved on while the request was out keeps their place.
    if (active && active !== document.body && active !== startedOn) {
      return;
    }

    const wrong = error.fields
      .map((cause) => (cause.field ? fieldIds[cause.field] : undefined))
      .flatMap((id) => (id ? [document.getElementById(id)] : []))
      .filter((control) => control !== null);

    wrong.sort((a, b) =>
      a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_FOLLOWING ? -1 : 1
    );
    wrong[0]?.focus();
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
      // A scan over a handful of causes beats a map kept in step with the failure.
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

      const startedOn = globalThis.document?.activeElement ?? null;
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

      focusFirstError(error, startedOn);

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
