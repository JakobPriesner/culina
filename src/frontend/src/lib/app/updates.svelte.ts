import { busy } from './busy.svelte';
import { readDevice, writeDevice } from './deviceStorage';
import { m } from './i18n';
import { toaster } from './toaster.svelte';

/**
 * Notices a new version and offers it, rather than imposing it.
 *
 * The service worker deliberately does not take over on its own: swapping the
 * app's code out from under a running page means the next chunk it loads comes
 * from a build that no longer matches what is on screen, and it breaks in the
 * middle of whatever was being done. In a kitchen that is someone's hands
 * covered in flour, halfway through step four.
 *
 * So the new version waits and is offered — once. A toast says it is there,
 * briefly, and then gets out of the way of the recipe underneath it; the offer
 * itself stays in settings until it is taken, and the version installs itself
 * the next time the app is opened anyway. A prompt that sat over the bottom of
 * every screen, and came back on every reload, was the most-seen thing in the
 * app and the least useful.
 *
 * And it is not mentioned at all while somebody is cooking or editing. An offer
 * is still an interruption, and "there is a new version" is never worth reading
 * with your hands in a bowl.
 */

/** Where the built worker is served from. */
const workerUrl = '/service-worker.js';

/**
 * How often to ask whether there is a newer version.
 *
 * Hourly, not on every navigation: this is a self-hosted app updated when its
 * owner deploys, not a site that ships ten times a day, and a request per page
 * view would be a heartbeat nobody asked for.
 */
const checkEveryMs = 60 * 60 * 1000;

/** Long enough to read and reach for, then out of the way. */
const mentionForMs = 12_000;

/** The last version this device was told about. */
const mentionedKey = 'culina.update.mentioned';

/** How long a waiting worker has to say which version it is. */
const versionTimeoutMs = 1000;

/** Whether a new version is waiting, and the way to it. */
class UpdateOffer {
  #waiting = $state<ServiceWorker | null>(null);

  get ready(): boolean {
    return this.#waiting !== null;
  }

  /** Remembers what is waiting. Returns false for the one already known. */
  hold(waiting: ServiceWorker): boolean {
    if (this.#waiting === waiting) {
      return false;
    }

    this.#waiting = waiting;

    return true;
  }

  /** Hands over to the waiting version and reloads into it. */
  apply(): void {
    const waiting = this.#waiting;

    if (!waiting) {
      return;
    }

    // The page reloads once the new worker has taken control, so the reload
    // is served by the build the person just agreed to.
    navigator.serviceWorker.addEventListener('controllerchange', () => location.reload(), {
      once: true
    });

    waiting.postMessage({ type: 'culina:activate' });
  }

  /** Called by tests. */
  reset(): void {
    this.#waiting = null;
  }
}

export const update = new UpdateOffer();

/** Starts the worker and watches for a replacement. Returns a stop function. */
export function watchForUpdates(): () => void {
  if (!('serviceWorker' in navigator)) {
    return () => {};
  }

  let stopped = false;
  let timer: ReturnType<typeof setInterval> | undefined;

  void navigator.serviceWorker
    .register(workerUrl, { type: 'module' })
    .then((registration) => {
      if (stopped) {
        return;
      }

      // A worker already waiting means the app was updated while it was
      // closed, and the page that opened it is still running the old build.
      offerIfWaiting(registration);

      registration.addEventListener('updatefound', () => {
        const installing = registration.installing;

        installing?.addEventListener('statechange', () => {
          // `installed` with a controller means a replacement, not a first
          // install: the very first worker has nothing to interrupt.
          if (installing.state === 'installed' && navigator.serviceWorker.controller) {
            void offer(installing);
          }
        });
      });

      timer = setInterval(() => void registration.update(), checkEveryMs);
    })
    .catch(() => {
      // An unregistrable worker is not a reason to fail: the app works without
      // one, it simply will not open offline.
    });

  return () => {
    stopped = true;

    if (timer !== undefined) {
      clearInterval(timer);
    }
  };
}

function offerIfWaiting(registration: ServiceWorkerRegistration): void {
  if (registration.waiting && navigator.serviceWorker.controller) {
    void offer(registration.waiting);
  }
}

/**
 * Keeps the offer, and mentions it if this device has not heard of it yet.
 *
 * However many times the browser reports the same waiting worker — on open,
 * on the hourly check, in a second tab — there is one offer, and one toast per
 * version for the life of the device.
 */
async function offer(waiting: ServiceWorker): Promise<void> {
  if (!update.hold(waiting)) {
    return;
  }

  const version = await versionOf(waiting);

  if (version !== null && readDevice(mentionedKey) === version) {
    return;
  }

  mentionWhenFree(() => {
    if (version !== null) {
      writeDevice(mentionedKey, version);
    }

    toaster.show({
      message: m['app.update.available'],
      durationMs: mentionForMs,
      action: { label: m['app.update.reload'], run: () => update.apply() }
    });
  });
}

/** Runs now, or once cooking or editing is over. */
function mentionWhenFree(mention: () => void): void {
  if (busy.interruptible) {
    mention();

    return;
  }

  // `$effect.root` because this runs outside a component: the worker's event,
  // not a render.
  const stop = $effect.root(() => {
    $effect(() => {
      if (busy.interruptible) {
        stop();
        mention();
      }
    });
  });
}

/**
 * Which build the waiting worker is, or null when it does not say.
 *
 * A worker from before it could answer never will, which is what the timeout
 * is for: that one is mentioned, just not remembered.
 */
function versionOf(waiting: ServiceWorker): Promise<string | null> {
  return new Promise((resolve) => {
    const channel = new MessageChannel();
    const timer = setTimeout(() => resolve(null), versionTimeoutMs);

    channel.port1.onmessage = (event: MessageEvent) => {
      clearTimeout(timer);
      resolve(typeof event.data === 'string' ? event.data : null);
    };

    waiting.postMessage({ type: 'culina:version' }, [channel.port2]);
  });
}
