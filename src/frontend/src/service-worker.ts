/// <reference lib="webworker" />
// The `$service-worker` module is declared in service-worker.d.ts, which
// tsconfig.worker.json names alongside this file. `pnpm check` runs it: the
// app's own tsconfig excludes the worker, and an unchecked worker once shipped
// a constant that was never declared.

import { version } from '$service-worker';
import { forgetKitchen } from './lib/features/cooking/timerState';
import { apiResponse, isApi, policyFor } from './service-worker/apiCache';
import { assetResponse, precache, shellResponse } from './service-worker/assets';
import { handleNotificationClick, handlePush } from './service-worker/notifications';
import { cacheName, privateCacheName, worker } from './service-worker/scope';
import { isShareTarget, shareTargetResponse } from './service-worker/shareTarget';

/**
 * The offline shell.
 *
 * Culina is a kitchen app: the phone is on a counter, the hands are wet, and
 * the wifi reaches the kitchen about as well as it reaches the garden. What
 * this worker guarantees is that the app itself always opens — the shell, the
 * fonts, the icons — so a dropped connection shows Culina saying it cannot
 * reach the server, rather than the browser's dinosaur.
 *
 * It keeps one narrow exception for the API, in its own cache: a recipe you
 * have opened stays readable and cookable without a network. Everything else
 * under `/api` goes to the network and nowhere else — nothing that changes
 * anything is ever answered from a cache, and the exception is a list of exact
 * shapes (`service-worker/apiCache.ts`) rather than a rule anyone could widen
 * by accident.
 *
 * That cache holds somebody's data on what may be a shared kitchen tablet, so
 * it is not allowed to outlive a session: the app empties it when anyone signs
 * in or out (see `culina:forget`), and the worker empties it itself the first
 * time the server answers one of its reads with a 401. Should both of those
 * miss, every copy still says whose it is (see `ownerHeader`).
 *
 * This file is the lifecycle and the dispatcher; what answers a request lives
 * in `service-worker/`: the asset and shell caches, the API cache, the share
 * target and the notifications.
 */

worker.addEventListener('install', (event) => {
  // No skipWaiting: the running app decides when to hand over. See the message
  // handler below.
  event.waitUntil(
    precache().catch((failure: unknown) => {
      // Still a failed install — an app that cannot open offline is not worth
      // installing — but now one somebody hears about.
      tell(failure);
      throw failure;
    })
  );
});

/**
 * Hands a failure to an open page, which reports it.
 *
 * Not reported from here: the worker never talks to the API (see the top of
 * this file), and it has no CSRF token to do it with. One page rather than
 * every open one, so three tabs do not make three records. With no page open
 * the failure goes unheard, which is the trade for keeping the worker out of
 * the API.
 */
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
  // Every earlier build's cache goes, the private one stays: its lifetime is
  // a session, and a deploy is not the end of one.
  const kept = new Set([cacheName, privateCacheName]);

  event.waitUntil(
    caches
      .keys()
      .then((keys) =>
        Promise.all(keys.filter((key) => !kept.has(key)).map((key) => caches.delete(key)))
      )
      // Existing tabs are controlled straight away, so the first load
      // after an update is already served by the worker that matches it.
      .then(() => worker.clients.claim())
  );
});

/**
 * Takes over when the page says it is ready, never before.
 *
 * A service worker that calls `skipWaiting()` on install replaces the running
 * app's assets underneath it: the next lazy-loaded chunk is from a build that
 * no longer matches what is on screen, and the app breaks in the middle of a
 * recipe. Culina shows a quiet "there is a new version" instead and reloads
 * when the person says so.
 */
worker.addEventListener('message', (event) => {
  const type = (event.data as { type?: string } | null)?.type;

  if (type === 'culina:activate') {
    void worker.skipWaiting();
  }

  // Asked by a page deciding whether it has already mentioned this version,
  // so an update is offered once rather than on every reload.
  if (type === 'culina:version') {
    event.ports[0]?.postMessage(version);
  }

  // Sent when anyone signs in or out. Both, not just out: a device where one
  // person closed the browser without signing out and another signed in must
  // not answer the second one from the first one's cache.
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

  // A navigation is always the same SPA shell: there is no server-rendered
  // HTML to be stale about, and answering from the cache is what makes the
  // app open with no network at all.
  if (request.mode === 'navigate') {
    event.respondWith(shellResponse());

    return;
  }

  event.respondWith(assetResponse(event, url));
});
