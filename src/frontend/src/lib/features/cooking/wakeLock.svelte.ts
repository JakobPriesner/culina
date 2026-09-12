/**
 * Keeps the screen on while somebody is cooking.
 *
 * A phone that sleeps between steps has to be woken with a wet hand, which is
 * the one thing a kitchen app must not make anyone do.
 *
 * The lock is released by the browser whenever the tab is hidden, so it has to
 * be retaken when the tab comes back — that is not an edge case, it is what
 * happens every time somebody checks a message.
 */
export function createWakeLock() {
  let held = $state(false);
  let sentinel: WakeLockSentinel | null = null;

  const supported = () => typeof navigator !== 'undefined' && 'wakeLock' in navigator;

  async function take() {
    if (!supported() || sentinel) {
      return;
    }

    try {
      sentinel = await navigator.wakeLock.request('screen');
      held = true;

      sentinel.addEventListener('release', () => {
        held = false;
        sentinel = null;
      });
    } catch {
      // Refused: a low battery, a policy, an unsupported browser. Cooking still
      // works, so this is never worth interrupting anyone about.
      held = false;
    }
  }

  async function release() {
    await sentinel?.release();
    sentinel = null;
    held = false;
  }

  return {
    /** Whether the screen is actually being held awake. */
    get held() {
      return held;
    },

    get supported() {
      return supported();
    },

    /** Starts holding, and keeps holding across tab switches. Returns the stop. */
    engage(): () => void {
      void take();

      const retake = () => {
        if (document.visibilityState === 'visible') {
          void take();
        }
      };

      document.addEventListener('visibilitychange', retake);

      return () => {
        document.removeEventListener('visibilitychange', retake);
        void release();
      };
    }
  };
}
