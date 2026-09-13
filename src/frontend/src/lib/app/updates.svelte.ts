import { busy } from './busy.svelte';
import { toaster } from './toaster.svelte';
import { m } from './i18n';

/**
 * Notices a new version and offers it, rather than imposing it.
 *
 * The service worker deliberately does not take over on its own: swapping the
 * app's code out from under a running page means the next chunk it loads comes
 * from a build that no longer matches what is on screen, and it breaks in the
 * middle of whatever was being done. In a kitchen that is someone's hands
 * covered in flour, halfway through step four.
 *
 * So the new version waits, a toast says it is there, and the reload happens
 * when the person taps it — or, at the latest, the next time they open the app.
 *
 * And it is not mentioned at all while somebody is cooking or editing. An offer
 * is still an interruption, and "there is a new version" is never worth reading
 * with your hands in a bowl. It waits for the next safe moment, which may be
 * the next time the app is opened.
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
            offer(installing);
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
    offer(registration.waiting);
  }
}

/**
 * One toast, until it is acted on.
 *
 * It does not expire: a version that is ready and never mentioned again is a
 * version that is never installed.
 */
function offer(waiting: ServiceWorker): void {
  if (!busy.interruptible) {
    // Asked again when cooking or editing is over. `$effect.root` because this
    // runs outside a component: the worker's event, not a render.
    const stop = $effect.root(() => {
      $effect(() => {
        if (busy.interruptible) {
          stop();
          offer(waiting);
        }
      });
    });

    return;
  }

  toaster.show({
    message: m['app.update.available'](),
    durationMs: 0,
    action: {
      label: m['app.update.reload'](),
      run: () => {
        // The page reloads once the new worker has taken control, so the
        // reload is served by the build the person just agreed to.
        navigator.serviceWorker.addEventListener('controllerchange', () => location.reload(), {
          once: true
        });

        waiting.postMessage({ type: 'culina:activate' });
      }
    }
  });
}
