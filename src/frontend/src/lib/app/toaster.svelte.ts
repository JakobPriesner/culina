/**
 * The queue of short messages, and the undo that makes them worth having.
 *
 * This is how Culina avoids confirmation dialogs. Asking "are you sure?" before
 * something reversible costs everyone a decision to protect against a mistake
 * that was already cheap to fix. Doing it and offering Undo costs nothing and
 * is faster for the person who meant it.
 */
export type ToastTone = 'neutral' | 'success' | 'danger';

export interface ToastAction {
  readonly label: string;
  readonly run: () => void;
}

export interface Toast {
  readonly id: string;
  readonly message: string;
  readonly tone: ToastTone;
  readonly action?: ToastAction;
  /** How long it stays. Zero means until it is dismissed. */
  readonly durationMs: number;
}

export interface ToastRequest {
  readonly message: string;
  readonly tone?: ToastTone;
  readonly action?: ToastAction;
  readonly durationMs?: number;
}

/** Long enough to read a sentence and reach for Undo, short enough to not nag. */
const defaultDurationMs = 6000;

/** More than three stacked is a log, not a notification. */
const maximum = 3;

class Toaster {
  #toasts = $state<Toast[]>([]);

  #timers = new Map<string, ReturnType<typeof setTimeout>>();

  get toasts(): readonly Toast[] {
    return this.#toasts;
  }

  show(request: ToastRequest): string {
    const toast: Toast = {
      id: crypto.randomUUID(),
      tone: 'neutral',
      durationMs: defaultDurationMs,
      ...request
    };

    // The oldest goes, not the newest: the most recent message is the one
    // about what just happened.
    this.#toasts = [...this.#toasts, toast].slice(-maximum);
    this.resume(toast.id);

    return toast.id;
  }

  dismiss(id: string): void {
    this.#clear(id);
    this.#toasts = this.#toasts.filter((toast) => toast.id !== id);
  }

  /**
   * Runs the action and takes the message away.
   *
   * Leaving an Undo on screen after it has been used invites a second press
   * that would undo the undo.
   */
  act(id: string): void {
    this.#toasts.find((toast) => toast.id === id)?.action?.run();
    this.dismiss(id);
  }

  /** Stops the clock while a pointer or focus is on the toast. */
  pause(id: string): void {
    this.#clear(id);
  }

  /** Restarts the full duration: the reading was interrupted, so give it again. */
  resume(id: string): void {
    const toast = this.#toasts.find((candidate) => candidate.id === id);

    if (!toast || toast.durationMs <= 0) {
      return;
    }

    this.#clear(id);
    this.#timers.set(
      id,
      setTimeout(() => this.dismiss(id), toast.durationMs)
    );
  }

  /** Called on sign-out, and by tests. */
  reset(): void {
    for (const id of [...this.#timers.keys()]) {
      this.#clear(id);
    }

    this.#toasts = [];
  }

  #clear(id: string): void {
    const timer = this.#timers.get(id);

    if (timer !== undefined) {
      clearTimeout(timer);
      this.#timers.delete(id);
    }
  }
}

export const toaster = new Toaster();
