import type { Snippet } from 'svelte';

/** The queue of short messages with Undo, which is how Culina avoids confirmation dialogs. */
export type ToastTone = 'neutral' | 'success' | 'danger';

/**
 * What a toast says, asked on each draw: a function so a toast raised in one language redraws in
 * another when it changes.
 */
export type ToastWords = () => string;

export interface ToastAction {
  readonly label: ToastWords;
  readonly run: () => void;
}

export interface Toast {
  readonly id: string;
  readonly message: ToastWords;
  readonly tone: ToastTone;
  readonly action?: ToastAction;
  readonly art?: Snippet;
  readonly durationMs: number;
}

export interface ToastRequest {
  readonly message: ToastWords;
  readonly tone?: ToastTone;
  readonly action?: ToastAction;
  readonly art?: Snippet;
  readonly durationMs?: number;
}

const defaultDurationMs = 6000;

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

    // The oldest goes: the newest is about what just happened.
    this.#toasts = [...this.#toasts, toast].slice(-maximum);
    this.resume(toast.id);

    return toast.id;
  }

  dismiss(id: string): void {
    this.#clear(id);
    this.#toasts = this.#toasts.filter((toast) => toast.id !== id);
  }

  /** Runs the action and takes the message away, so a used Undo can't be pressed twice. */
  act(id: string): void {
    this.#toasts.find((toast) => toast.id === id)?.action?.run();
    this.dismiss(id);
  }

  pause(id: string): void {
    this.#clear(id);
  }

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
