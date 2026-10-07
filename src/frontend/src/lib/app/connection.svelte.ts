/** Whether there is a network, as far as the browser can tell; `navigator.onLine` is weak, so it only explains why a save failed and never decides whether to try. */
class Connection {
  #online = $state(true);

  get online(): boolean {
    return this.#online;
  }

  /** Follows the device; returns a stop function. */
  start(): () => void {
    if (typeof window === 'undefined') {
      return () => {};
    }

    this.#online = navigator.onLine;

    const update = () => {
      this.#online = navigator.onLine;
    };

    window.addEventListener('online', update);
    window.addEventListener('offline', update);

    return () => {
      window.removeEventListener('online', update);
      window.removeEventListener('offline', update);
    };
  }
}

export const connection = new Connection();

/** Empties the cache of recipes this device has read, on sign-in and sign-out, so a shared tablet never answers one person from another's cache. */
export function forgetCachedReads(): void {
  if (typeof navigator === 'undefined' || !('serviceWorker' in navigator)) {
    return;
  }

  void navigator.serviceWorker.ready
    .then((registration) => registration.active?.postMessage({ type: 'culina:forget' }))
    .catch(() => {
      // No worker, nothing cached, nothing to forget.
    });
}
