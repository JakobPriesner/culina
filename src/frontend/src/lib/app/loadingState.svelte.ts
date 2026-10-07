/**
 * The timing rules for showing loading, written once: nothing for ~150 ms, then at least ~300 ms
 * visible to avoid flicker,
 * and after ~10 s it says so and offers a retry.
 */
export interface LoadingTimings {
  readonly delayMs: number;
  readonly minimumMs: number;
  readonly slowMs: number;
}

export const defaultTimings: LoadingTimings = {
  delayMs: 150,
  minimumMs: 300,
  slowMs: 10_000
};

export interface LoadingState {
  readonly showing: boolean;
  readonly slow: boolean;
  start(): void;
  stop(): void;
  dispose(): void;
}

export function createLoadingState(timings: LoadingTimings = defaultTimings): LoadingState {
  let showing = $state(false);
  let slow = $state(false);

  let shownAt = 0;
  let running = false;
  const timers: ReturnType<typeof setTimeout>[] = [];

  const clear = () => {
    for (const timer of timers.splice(0)) {
      clearTimeout(timer);
    }
  };

  const after = (ms: number, run: () => void) => timers.push(setTimeout(run, ms));

  return {
    get showing() {
      return showing;
    },

    get slow() {
      return slow;
    },

    start() {
      if (running) {
        return;
      }

      running = true;
      clear();

      after(timings.delayMs, () => {
        if (!running) {
          return;
        }

        showing = true;
        shownAt = Date.now();

        after(timings.slowMs, () => {
          if (running) {
            slow = true;
          }
        });
      });
    },

    stop() {
      running = false;
      clear();

      if (!showing) {
        slow = false;

        return;
      }

      // Already visible: hold it for the rest of the minimum so it can't appear and vanish in one
      // glance.
      const remaining = timings.minimumMs - (Date.now() - shownAt);

      if (remaining <= 0) {
        showing = false;
        slow = false;

        return;
      }

      after(remaining, () => {
        showing = false;
        slow = false;
      });
    },

    dispose() {
      running = false;
      clear();
      showing = false;
      slow = false;
    }
  };
}
