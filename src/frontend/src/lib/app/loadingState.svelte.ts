/**
 * The timing rules for showing that something is loading — written once.
 *
 * Three numbers, each earning its place:
 *
 * - Nothing appears for the first ~150 ms. Most responses arrive inside that,
 *   and a skeleton that flashes for one frame makes a fast app feel broken.
 * - Once shown, it stays for at least ~300 ms. Otherwise a response landing at
 *   160 ms produces a flicker that reads as a glitch rather than as progress.
 * - After ~10 s it says so and offers to try again, instead of spinning
 *   forever while the person wonders whether to reload.
 *
 * Every feature uses this rather than its own `setTimeout`, because the moment
 * two screens disagree about these numbers the app feels inconsistent and
 * nobody can say why.
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
  /** True only once the delay has passed and the minimum has not yet elapsed. */
  readonly showing: boolean;
  /** True when it has been long enough to say "this is taking a while". */
  readonly slow: boolean;
  /** Call when the work starts. */
  start(): void;
  /** Call when the work finishes, successfully or not. */
  stop(): void;
  /** Call when the component goes away, so nothing fires into a dead tree. */
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

      // Already visible: hold it for the rest of the minimum so it cannot
      // appear and vanish within the same glance.
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
