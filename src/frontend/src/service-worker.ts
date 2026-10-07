/// <reference lib="webworker" />
// `$service-worker` is declared in service-worker.d.ts; tsconfig.worker.json type-checks the worker
// because the app's tsconfig excludes it.

import { version } from '$service-worker';
import { forgetKitchen } from './lib/features/cooking/timerState';
import { apiResponse, isApi, policyFor } from './service-worker/apiCache';
import { assetResponse, precache, shellResponse } from './service-worker/assets';
import { handleNotificationClick, handlePush } from './service-worker/notifications';
import { cacheName, privateCacheName, worker } from './service-worker/scope';
import { isShareTarget, shareTargetResponse } from './service-worker/shareTarget';

/**
 * The offline shell: the app always opens offline. The API is network-only except the exact reads
 * in `service-worker/apiCache.ts`, and that private cache must not outlive a session: it is emptied on
 * sign-in/out (`culina:forget`) and on a 401, and each copy carries an owner (`ownerHeader`) as a backstop.
 * This file is the lifecycle and dispatcher; handlers live in `service-worker/`.
 */

worker.addEventListener('install', (event) => {
  // No skipWaiting: the running app decides when to hand over (see the message handler).
  event.waitUntil(
    precache().catch((failure: unknown) => {
      // Still a failed install, but now a reported one.
      tell(failure);
      throw failure;
    })
  );
});

/** Hands a failure to one open page to report: the worker never calls the API and has no CSRF token. Unheard if no page is open. */
function tell(failure: unknown): void {
  const message = String(failure);
  const stack = failure instanceof Error ? failure.stack : undefined;

  void worker.clients
    .matchAll({ type: 'window', includeUncontrolled: true })
    .then(([page]) => page?.postMessage({ type: 'culina:failed', message, stack }));
}

worker.addEventListener('error', (event) => tell(event.error ?? event.message));
worker.addEventListener('unhandledrejection', (event) => tell(event.reason));

worker.addEventListener('activate', (event) => {
  // Drop earlier builds' caches; the private one lives per session, not per deploy.
  const kept = new Set([cacheName, privateCacheName]);

  event.waitUntil(
    caches
      .keys()
      .then((keys) =>
        Promise.all(keys.filter((key) => !kept.has(key)).map((key) => caches.delete(key)))
      )
      // Control existing tabs now so the next load matches this worker.
      .then(() => worker.clients.claim())
  );
});

/** Takes over only when the page says so: an early `skipWaiting()` swaps assets under the running app and breaks lazy chunks. */
worker.addEventListener('message', (event) => {
  const type = (event.data as { type?: string } | null)?.type;

  if (type === 'culina:activate') {
    void worker.skipWaiting();
  }

  // Lets the page offer each update once.
  if (type === 'culina:version') {
    event.ports[0]?.postMessage(version);
  }

  // Sent on sign-in and sign-out: a browser closed without signing out must not serve the next user.
  if (type === 'culina:forget') {
    event.waitUntil(Promise.all([caches.delete(privateCacheName), forgetKitchen()]));
  }
});

worker.addEventListener('push', handlePush);
worker.addEventListener('notificationclick', handleNotificationClick);

worker.addEventListener('fetch', (event) => {
  const request = event.request;
  const url = new URL(request.url);

  if (isShareTarget(request, url, worker.location.origin)) {
    event.respondWith(shareTargetResponse(request, url));
    return;
  }

  if (request.method !== 'GET') {
    return;
  }

  if (url.origin !== worker.location.origin) {
    return;
  }

  if (isApi(url)) {
    const policy = policyFor(url);

    if (policy) {
      event.respondWith(apiResponse(event, policy));
    }

    return;
  }

  // Navigations are always the SPA shell, so serving it from cache works offline.
  if (request.mode === 'navigate') {
    event.respondWith(shellResponse());

    return;
  }

  event.respondWith(assetResponse(event, url));
});
