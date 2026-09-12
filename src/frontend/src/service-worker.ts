/// <reference lib="webworker" />
// The `$service-worker` module is declared in service-worker.d.ts, which
// tsconfig.worker.json names alongside this file. `pnpm check` runs it: the
// app's own tsconfig excludes the worker, and an unchecked worker once shipped
// a constant that was never declared.

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
 * It keeps one narrow exception for the API, in its own cache: a recipe you
 * have opened stays readable and cookable without a network. Everything else
 * under `/api` goes to the network and nowhere else — nothing that changes
 * anything is ever answered from a cache, and the exception is a list of exact
 * shapes below rather than a rule anyone could widen by accident.
 *
 * That cache holds somebody's data on what may be a shared kitchen tablet, so
 * it is not allowed to outlive a session: the app empties it when anyone signs
 * in or out. See `culina:forget`.
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
 * Recipes that have been read, kept apart from the build's own files.
 *
 * Not named for the version: a deploy must not cost somebody the recipes they
 * are relying on being able to open. Its lifetime is a session, not a build.
 */
const privateCacheName = 'culina-private';

/**
 * How many recipe responses to keep.
 *
 * A number, because a cache with no limit is a disk-space bug waiting for the
 * person with three hundred recipes. Oldest written goes first, which for a
 * recipe collection is close enough to least used.
 */
const privateCacheLimit = 120;

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
  const type = (event.data as { type?: string } | null)?.type;

  if (type === 'culina:activate') {
    void worker.skipWaiting();
  }

  // Sent when anyone signs in or out. Both, not just out: a device where one
  // person closed the browser without signing out and another signed in must
  // not answer the second one from the first one's cache.
  if (type === 'culina:forget') {
    event.waitUntil(caches.delete(privateCacheName));
  }
});

worker.addEventListener('fetch', (event) => {
  const request = event.request;

  if (request.method !== 'GET') {
    return;
  }

  const url = new URL(request.url);

  if (url.origin !== worker.location.origin) {
    return;
  }

  if (isApi(url)) {
    const policy = policyFor(url);

    if (policy) {
      event.respondWith(apiResponse(request, policy));
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

  event.respondWith(assetResponse(request, url));
});

const isApi = (url: URL): boolean =>
  url.pathname === `${base}/api` || url.pathname.startsWith(`${base}/api/`);

/**
 * The only API reads that are ever kept, and how.
 *
 * An allow-list of exact shapes. A rule like "cache anything under /recipes"
 * would quietly start keeping whatever is added there next.
 */
type CachePolicy = 'cache-first' | 'stale-while-revalidate' | 'network-first';

const one = (pattern: RegExp, policy: CachePolicy) => ({ pattern, policy }) as const;

const readable = [
  // Content-addressed and immutable: the URL carries the width and the recipe's
  // version, so what is cached can never be the wrong picture.
  one(/^\/api\/v1\/recipes\/[^/]+\/image$/, 'cache-first'),
  // The recipe itself. Shown from the cache at once and refreshed behind it, so
  // opening a recipe is instant and still current a moment later.
  one(/^\/api\/v1\/recipes\/[^/]+$/, 'stale-while-revalidate'),
  // The list is the one screen where being out of date is visible, so the
  // network wins when there is one and the cache only catches a fall.
  one(/^\/api\/v1\/recipes$/, 'network-first'),
  // Who is signed in. Without it a cold start with no network cannot tell
  // "offline" from "signed out", and answers the second one — which locks
  // somebody out of the recipes this cache exists to have kept for them.
  // Never cached as a failure: a 401 is not `ok`, so an expired session is
  // still an expired session the moment the network comes back.
  one(/^\/api\/v1\/users\/me$/, 'stale-while-revalidate')
];

function policyFor(url: URL): CachePolicy | null {
  const path = url.pathname.slice(base.length);

  return readable.find((candidate) => candidate.pattern.test(path))?.policy ?? null;
}

/**
 * Answers a recipe read, keeping a copy for the next time there is no network.
 *
 * A cached response is returned as it was stored, headers and all, so the
 * client's own ETag handling sees exactly what the server sent.
 *
 * Nothing in here may throw. A rejected promise passed to `respondWith` reaches
 * the page as "Failed to fetch" — a network error the app cannot tell apart
 * from a dead wifi, for a request that actually succeeded.
 */
async function apiResponse(request: Request, policy: CachePolicy): Promise<Response> {
  const cache = await caches.open(privateCacheName).catch(() => null);
  const cached = cache ? await cache.match(request).catch(() => undefined) : undefined;

  if (cached && policy === 'cache-first') {
    return cached;
  }

  // Started once and awaited at most once more: a `Request` is spent by the
  // fetch that used it, so there is no second attempt to be had.
  const fresh = fetchAndStore(request, cache);

  if (cached && policy === 'stale-while-revalidate') {
    // Deliberately not awaited: the point of showing the cached copy is not
    // waiting for the network. The rejection is already handled inside.
    void fresh;

    return cached;
  }

  const response = await fresh;

  if (response) {
    return response;
  }

  if (cached) {
    return cached;
  }

  // Nothing cached and no network. The app has a good sentence for this; what
  // it needs from here is the ordinary failure, which is what re-throwing the
  // fetch gives it.
  return fetch(request.url, { credentials: 'include', headers: request.headers });
}

/** Fetches, stores what is worth storing, and never rejects. */
async function fetchAndStore(request: Request, cache: Cache | null): Promise<Response | null> {
  try {
    const response = await fetch(request);

    // A 304 carries no body to keep, and an error response cached is a fault
    // that outlives the deploy that fixed it.
    if (cache && response.ok && response.type === 'basic') {
      try {
        await cache.put(request, response.clone());
        await trim(cache);
      } catch {
        // Out of quota, or a response the Cache API will not take. Worth
        // nothing and worth failing over even less.
      }
    }

    return response;
  } catch {
    return null;
  }
}

/** Drops the oldest entries once the cache is over its limit. */
async function trim(cache: Cache): Promise<void> {
  const keys = await cache.keys();

  for (const stale of keys.slice(0, keys.length - privateCacheLimit)) {
    await cache.delete(stale);
  }
}

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
