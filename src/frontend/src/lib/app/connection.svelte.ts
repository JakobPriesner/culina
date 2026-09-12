/**
 * Whether there is a network, as far as the browser can tell.
 *
 * `navigator.onLine` is a weak signal — it says the device has a connection,
 * not that the server is reachable — so it is used for one thing only: telling
 * somebody why a change did not save. It never decides whether to try. A
 * request is always attempted, because "offline" is often wrong and a request
 * that would have worked is worse than a wasted one.
 */
class Connection {
  #online = $state(true);

  get online(): boolean {
    return this.#online;
  }

  /** Follows the device. Returns a stop function. */
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

/**
 * Empties the cache of recipes this device has read.
 *
 * Called on the way in and on the way out of a session. Both, because a shared
 * kitchen tablet where one person closed the browser without signing out must
 * not answer the next person from the first one's cache.
 */
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
