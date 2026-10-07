/*
 * The one narrow exception for the API: a list of exact reads that are kept, in
 * their own cache, so a recipe you have opened stays readable without a network.
 * Nothing that changes anything is ever answered from here.
 */
import { base } from '$service-worker';
import { forgetKitchen } from '../lib/features/cooking/timerState';
import { privateCacheName } from './scope';

/**
 * How many recipe responses to keep.
 *
 * A number, because a cache with no limit is a disk-space bug waiting for the
 * person with three hundred recipes. Oldest written goes first, which for a
 * recipe collection is close enough to least used.
 */
const privateCacheLimit = 120;

/**
 * Who is signed in, as far as the worker knows: the last answer the network
 * gave to this read, which is kept like the others.
 */
const whoIsSignedIn = `${base}/api/v1/users/me`;

/**
 * Names the user a kept copy was read for.
 *
 * The cache is keyed by address only, and `/recipes/r1` is the same address
 * for everybody. Emptying it on sign-in and sign-out depends on a message from
 * the page reaching the worker, which a tab closed straight after signing out
 * may never send. So every copy is stored with this header — the user
 * `whoIsSignedIn` named when the read began, or for that read itself the user
 * it answered with — and a copy is only ever answered to that same user. Once
 * the network has said somebody else is signed in, the last person's copies
 * are never shown again, offline or not; a copy with no owner is never shown.
 *
 * What it cannot do is tell two people apart while the network says nothing:
 * somebody picking up a tablet offline is, to the worker, whoever was signed
 * in last.
 */
const ownerHeader = 'X-Culina-Owner';

/**
 * How long a network-first read waits for the network when there is a copy.
 *
 * Without one, a stalled connection — the supermarket, the far end of the
 * kitchen — never rejects the fetch, so the cache was never reached: the page
 * gave up first and said the server was unavailable while the recipe sat in
 * the cache. Shorter than the four seconds the app waits for who is signed in
 * at boot, so the copy arrives before the app stops waiting for it.
 */
const networkDeadlineMs = 2_500;

export const isApi = (url: URL): boolean =>
  url.pathname === `${base}/api` || url.pathname.startsWith(`${base}/api/`);

/**
 * The only API reads that are ever kept, and how.
 *
 * An allow-list of exact shapes. A rule like "cache anything under /recipes"
 * would quietly start keeping whatever is added there next.
 */
export type CachePolicy = 'cache-first' | 'network-first';

const one = (pattern: RegExp, policy: CachePolicy) => ({ pattern, policy }) as const;

const readable = [
  // Content-addressed and immutable: the URL carries the width and the
  // picture's id, so what is cached can never be the wrong picture.
  one(/^\/api\/v1\/recipes\/[^/]+\/image$/, 'cache-first'),
  // The recipe, the list, and who is signed in: the network wins whenever it
  // answers in time, and the cache only ever catches a fall or a stall.
  //
  // Not stale-while-revalidate, which is the obvious choice and the wrong one.
  // Showing the stored copy first means that the moment after somebody edits a
  // recipe, opening it shows the version from before their edit — the app
  // arguing with itself about what it just saved. Opening a recipe is fast
  // enough without it, and what was promised is that a recipe stays *readable*
  // with no network, not that it appears instantly with one.
  one(/^\/api\/v1\/recipes\/[^/]+$/, 'network-first'),
  one(/^\/api\/v1\/recipes$/, 'network-first'),
  // The household shopping list: supermarkets often have poor or no cellular
  // reception. Network-first lets an offline device display the list as last seen.
  one(/^\/api\/v1\/households\/[^/]+\/shopping-list$/, 'network-first'),
  // Who is signed in. Without this a cold start with no network cannot tell
  // "offline" from "signed out", and answers the second one — which locks
  // somebody out of the recipes this cache exists to have kept for them.
  // Never cached as a failure: a 401 is not `ok`, so an expired session is
  // still an expired session the moment the network comes back.
  one(/^\/api\/v1\/users\/me$/, 'network-first')
  // Deliberately not `/api/v1/cookbooks`. What a cookbook promises offline is
  // the recipes on it, and those are the `/recipes` rule above — a cookbook's
  // own name and cover are chrome, and caching them would widen the offline
  // surface past anything that was promised.
];

export function policyFor(url: URL): CachePolicy | null {
  const path = url.pathname.slice(base.length);
  const policy = readable.find((candidate) => candidate.pattern.test(path))?.policy ?? null;

  // Cache first is only true of an address that names its content. A picture
  // asked for without `v` is whatever the recipe has now, and keeping that
  // forever showed a replaced picture forever. Left to the browser instead,
  // it revalidates against the image's ETag and costs a 304.
  if (policy === 'cache-first' && !url.searchParams.has('v')) {
    return null;
  }

  return policy;
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
export async function apiResponse(event: FetchEvent, policy: CachePolicy): Promise<Response> {
  const request = event.request;
  const cache = await caches.open(privateCacheName).catch(() => null);
  // Two independent reads of the same cache, so not one after the other.
  const [owner, kept] = cache
    ? await Promise.all([signedInAs(cache), cache.match(request).catch(() => undefined)])
    : [null, undefined];
  const cached = owner && kept?.headers.get(ownerHeader) === owner ? kept : undefined;

  if (cached && policy === 'cache-first') {
    return cached;
  }

  // Started once and awaited once: a `Request` is spent by the fetch that used
  // it, so there is no second attempt to be had. Kept alive past the answer,
  // so a slow network that loses to the deadline still refreshes the copy.
  const fresh = fetchAndStore(request, cache, owner);

  event.waitUntil(fresh);

  const response = cached ? await Promise.race([fresh, deadline()]) : await fresh;

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

/** Resolves with nothing once the network has had its chance. */
function deadline(): Promise<null> {
  return new Promise((resolve) => setTimeout(() => resolve(null), networkDeadlineMs));
}

/** Fetches, stores what is worth storing for `owner`, and never rejects. */
async function fetchAndStore(
  request: Request,
  cache: Cache | null,
  owner: string | null
): Promise<Response | null> {
  try {
    const response = await fetch(request);

    // The server saying there is no session. One that ended while the app was
    // closed never reached a sign-out, and the page does not end it either:
    // the 401 arrives at boot, before the app listens for an expiry. Without
    // this the next person's first offline start answered as the last one.
    if (response.status === 401) {
      await caches.delete(privateCacheName).catch(() => false);
      await forgetKitchen();

      return response;
    }

    // A 304 carries no body to keep, and an error response cached is a fault
    // that outlives the deploy that fixed it.
    //
    // A refresh that lands after `culina:forget` cannot bring the last
    // person's copy back: `cache` was opened before the delete, and a deleted
    // cache stays writable but is no longer the one `caches.open` returns.
    if (cache && response.ok && response.type === 'basic') {
      try {
        const readFor =
          new URL(request.url).pathname === whoIsSignedIn ? await userIdIn(response) : owner;

        if (readFor) {
          await cache.put(request, ownedCopy(response, readFor));
          await trim(cache);
        }
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

/** The user the kept `whoIsSignedIn` names, or nobody when there is none. */
async function signedInAs(cache: Cache): Promise<string | null> {
  const me = await cache.match(whoIsSignedIn).catch(() => undefined);

  return me?.headers.get(ownerHeader) ?? null;
}

async function userIdIn(response: Response): Promise<string | null> {
  const { userId } = (await response.clone().json()) as { userId?: unknown };

  return typeof userId === 'string' ? userId : null;
}

/** The response as it came, plus whose it is. */
function ownedCopy(response: Response, owner: string): Response {
  const headers = new Headers(response.headers);

  headers.set(ownerHeader, owner);

  return new Response(response.clone().body, {
    status: response.status,
    statusText: response.statusText,
    headers
  });
}

/** Drops the oldest entries once the cache is over its limit. */
async function trim(cache: Cache): Promise<void> {
  const keys = await cache.keys();

  for (const stale of keys.slice(0, keys.length - privateCacheLimit)) {
    await cache.delete(stale);
  }
}
