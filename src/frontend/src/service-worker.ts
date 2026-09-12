/// <reference types="@sveltejs/kit" />
/// <reference lib="webworker" />

import { base, build, files, version } from '$service-worker';

/**
 * The offline shell.
 *
 * Culina is a kitchen app: the phone is on a counter, the hands are wet, and
 * the wifi reaches the kitchen about as well as it reaches the garden. What
 * this worker guarantees is that the app itself always opens — the shell, the
 * fonts, the icons — so a dropped connection shows Culina saying it cannot
 * reach the server, rather than the browser's dinosaur.
 *
 * What it deliberately does *not* do is cache a single API response. A recipe
 * is somebody's data, the tablet in a shared kitchen is somebody else's device,
 * and a cache that outlives a sign-out is a data leak with no user-visible
 * cause. Reading recipes offline is a separate, deliberate opt-in with its own
 * lifecycle; until then, `/api` goes to the network and nowhere else.
 */
const worker = self as unknown as ServiceWorkerGlobalScope;

/**
 * One cache per build.
 *
 * `version` changes on every build, so a new worker fills a new cache and the
 * old one is deleted only once the new worker takes over. A half-updated cache
 * — new HTML, old JavaScript — is the failure mode that makes people uninstall
 * a PWA, and a cache per version makes it impossible.
 */
const cacheName = `culina-${version}`;

/**
 * The document every route renders into.
 *
 * The root path, not `/index.html`: that is what a navigation actually asks
 * for, and the host renders the shell there rather than serving a file — the
 * file name is an implementation detail of one particular way of hosting it.
 */
const document = `${base}/`;

/**
 * Everything else worth having before the network goes away.
 *
 * Storing the document also stores the CSP header that came with it, so the
 * nonce inside the cached shell and the nonce its policy names are the same
 * one — which is the whole reason the shell is rendered rather than served
 * from disk.
 */
const shell = [...build, ...files];

worker.addEventListener('install', (event) => {
  // No skipWaiting: the running app decides when to hand over. See the message
  // handler below.
  event.waitUntil(precache());
});

/**
 * Fills the cache for this build.
 *
 * The document has to be there — without it the app does not open offline at
 * all, and an install that cannot promise that is worth failing. Everything
 * else is best effort: `addAll` is all or nothing, so one file that 404s
 * because a deploy moved it would mean no offline app whatsoever, which is a
 * great deal worse than one uncached icon.
 */
async function precache(): Promise<void> {
  const cache = await caches.open(cacheName);

  await cache.add(document);
  await Promise.allSettled(shell.map((asset) => cache.add(asset)));
}

worker.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) =>
        Promise.all(keys.filter((key) => key !== cacheName).map((key) => caches.delete(key)))
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
  if ((event.data as { type?: string } | null)?.type === 'culina:activate') {
    void worker.skipWaiting();
  }
});

worker.addEventListener('fetch', (event) => {
  const request = event.request;

  if (request.method !== 'GET') {
    return;
  }

  const url = new URL(request.url);

  // Another origin, or the API. Neither is ours to cache.
  if (url.origin !== worker.location.origin || isApi(url)) {
    return;
  }

  // A navigation is always the same SPA shell: there is no server-rendered
  // HTML to be stale about, and answering from the cache is what makes the
  // app open with no network at all.
  if (request.mode === 'navigate') {
    event.respondWith(shellResponse());

    return;
  }

  event.respondWith(assetResponse(request, url));
});

const isApi = (url: URL): boolean =>
  url.pathname === `${base}/api` || url.pathname.startsWith(`${base}/api/`);

async function shellResponse(): Promise<Response> {
  const cache = await caches.open(cacheName);
  const cached = await cache.match(document);

  return cached ?? fetch(document);
}

/**
 * Cache first for the build's own assets, network first for the rest.
 *
 * A file under `build` has a hash in its name: its contents cannot change, so
 * asking the network about it is a round trip that can only ever return the
 * same bytes. A static file — an icon, the manifest — keeps its name across
 * builds, so the network's copy is the newer one when there is a network.
 */
async function assetResponse(request: Request, url: URL): Promise<Response> {
  const cache = await caches.open(cacheName);

  if (build.includes(url.pathname)) {
    const cached = await cache.match(request);

    if (cached) {
      return cached;
    }
  }

  try {
    const response = await fetch(request);

    // Opaque and error responses are not worth keeping: a cached 404
    // outlives the deploy that fixes it.
    if (response.ok && response.type === 'basic') {
      await cache.put(request, response.clone());
    }

    return response;
  } catch (offline) {
    const cached = await cache.match(request);

    if (cached) {
      return cached;
    }

    throw offline;
  }
}
