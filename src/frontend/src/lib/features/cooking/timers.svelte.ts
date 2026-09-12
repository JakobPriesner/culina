/**
 * Kitchen timers.
 *
 * Deliberately not on the server. A timer must keep counting while the app is
 * closed and the phone is in a pocket, which means it has to be a wall-clock
 * deadline stored on the device — not a duration ticked down by a page that may
 * not be running. A cooking session is a fact worth persisting; a timer is
 * local ephemera.
 */
export interface KitchenTimer {
  /** Which step it belongs to. */
  readonly stepIndex: number;
  /** When it goes off, as an absolute time. */
  readonly endsAt: number;
  readonly label: string;
}

const storageKey = (sessionId: string) => `culina.timers.${sessionId}`;

export function createTimers(sessionId: () => string | null) {
  let timers = $state<KitchenTimer[]>([]);
  let now = $state(Date.now());

  let ticking: ReturnType<typeof setInterval> | undefined;

  /** Seconds left, or zero once it has gone off. Never negative. */
  const remaining = (timer: KitchenTimer): number =>
    Math.max(0, Math.round((timer.endsAt - now) / 1000));

  function persist() {
    const id = sessionId();

    if (!id) {
      return;
    }

    try {
      localStorage.setItem(storageKey(id), JSON.stringify(timers));
    } catch {
      // A blocked store costs the timer its persistence, not its tick.
    }
  }

  return {
    get timers() {
      return timers;
    },

    remaining,

    /** True once a timer has gone off and has not been dismissed. */
    isDone: (timer: KitchenTimer) => remaining(timer) === 0,

    /** Reads back whatever was still running, discarding what has expired. */
    load() {
      const id = sessionId();

      if (!id) {
        return;
      }

      try {
        const raw = localStorage.getItem(storageKey(id));
        const stored = raw ? (JSON.parse(raw) as KitchenTimer[]) : [];

        // A wall-clock deadline is still correct after the app was closed for
        // an hour; a duration would not have been.
        timers = stored.filter((timer) => typeof timer.endsAt === 'number');
      } catch {
        timers = [];
      }
    },

    start(stepIndex: number, seconds: number, label: string) {
      timers = [
        ...timers.filter((timer) => timer.stepIndex !== stepIndex),
        { stepIndex, endsAt: Date.now() + seconds * 1000, label }
      ];

      persist();
    },

    dismiss(stepIndex: number) {
      timers = timers.filter((timer) => timer.stepIndex !== stepIndex);
      persist();
    },

    /** One interval for every timer, rather than one each. */
    tick(): () => void {
      ticking = setInterval(() => (now = Date.now()), 1000);

      return () => clearInterval(ticking);
    },

    clear() {
      const id = sessionId();

      timers = [];

      if (id) {
        try {
          localStorage.removeItem(storageKey(id));
        } catch {
          // Nothing to do.
        }
      }
    }
  };
}
