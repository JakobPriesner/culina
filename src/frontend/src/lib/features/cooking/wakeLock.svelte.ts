/** The installed app's keep-awake, which holds for as long as it is asked (no browser to release it behind our back). */
export interface NativeWakeLock {
  hold(): Promise<void>;
  release(): Promise<void>;
}

let nativeLock: NativeWakeLock | null = null;

/** Registered by the installed app (lib/app/nativeShell.ts): the web view's own Wake Lock is not dependable there. */
export function useNativeWakeLock(lock: NativeWakeLock): void {
  nativeLock = lock;
}

/** Keeps a cooking session awake; browser release is reflected immediately. */
export function createWakeLock() {
  let held = $state(false);
  let sentinel: WakeLockSentinel | null = null;
  let generation = 0;
  let engaged = false;
  let requesting = false;
  const supported = () =>
    nativeLock !== null || (typeof navigator !== 'undefined' && 'wakeLock' in navigator);

  async function take() {
    if (
      !engaged ||
      !supported() ||
      sentinel ||
      requesting ||
      document.visibilityState !== 'visible'
    )
      return;
    requesting = true;
    const version = generation;
    if (nativeLock) {
      const lock = nativeLock;
      try {
        await lock.hold();
        held = engaged && version === generation;
        if (!held) await lock.release();
      } catch {
        held = false;
      } finally {
        requesting = false;
      }
      return;
    }
    try {
      const lock = await navigator.wakeLock.request('screen');
      if (!engaged || version !== generation) {
        await lock.release();
        return;
      }
      sentinel = lock;
      held = !lock.released;
      lock.addEventListener('release', () => {
        if (sentinel === lock) {
          held = false;
          sentinel = null;
        }
      });
    } catch {
      held = false;
    } finally {
      requesting = false;
      // A new session may have engaged while the old request was in flight.
      if (engaged && version !== generation) void take();
    }
  }

  return {
    get held() {
      return held;
    },
    get supported() {
      return supported();
    },
    engage(): () => void {
      engaged = true;
      generation++;
      void take();
      const retake = () => {
        if (document.visibilityState === 'visible') void take();
      };
      document.addEventListener('visibilitychange', retake);
      return () => {
        engaged = false;
        generation++;
        document.removeEventListener('visibilitychange', retake);
        const lock = sentinel;
        const wasHeld = held;
        sentinel = null;
        held = false;
        void lock?.release().catch(() => {});
        if (nativeLock && wasHeld) void nativeLock.release().catch(() => {});
      };
    }
  };
}
