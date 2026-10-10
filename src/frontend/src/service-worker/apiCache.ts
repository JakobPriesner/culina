/* The narrow API exception: exact reads kept so opened recipes stay readable offline. Writes are never answered from here. */
import { base } from '$service-worker';
import { forgetKitchen } from '../lib/features/cooking/timerState';
import { privateCacheName } from './scope';

const privateCacheLimit = 120;

const whoIsSignedIn = `${base}/api/v1/users/me`;

/**
 * Names the user a kept copy was read for; a copy is only answered to that user, never an unowned one.
 * Needed because the cache is keyed by address alone and the sign-out message may never arrive.
 */
const ownerHeader = 'X-Culina-Owner';

/** How long a network-first read waits when a copy exists; a stalled connection never rejects. Under the app's 4s boot wait. */
const networkDeadlineMs = 2_500;

export const isApi = (url: URL): boolean =>
  url.pathname === `${base}/api` || url.pathname.startsWith(`${base}/api/`);

/** How a kept read is answered; the allow-list below holds exact shapes so new routes are never cached by accident. */
export type CachePolicy = 'cache-first' | 'network-first';

const one = (pattern: RegExp, policy: CachePolicy) => ({ pattern, policy }) as const;

const readable = [
  // Content-addressed and immutable: the URL carries the width and picture id.
  one(/^\/api\/v1\/recipes\/[^/]+\/image$/, 'cache-first'),
  // Network wins when it answers in time. Not stale-while-revalidate: it would show the
  // pre-edit recipe right after saving one.
  one(/^\/api\/v1\/recipes\/[^/]+$/, 'network-first'),
  // An opened recipe is readable offline, and so is what it has in it. The recipe's ETag is its version alone,
  // so a household's correction would hide behind a 304 there; this one is its own resource with its own tag.
  one(/^\/api\/v1\/recipes\/[^/]+\/nutrition$/, 'network-first'),
  one(/^\/api\/v1\/recipes$/, 'network-first'),
  // Shopping list: supermarkets often have no reception.
  one(/^\/api\/v1\/households\/[^/]+\/shopping-list$/, 'network-first'),
  // Without this an offline cold start cannot tell "offline" from "signed out". A 401 is never cached.
  one(/^\/api\/v1\/users\/me$/, 'network-first')
  // Deliberately not `/api/v1/cookbooks`: offline promises the recipes, not cookbook chrome.
];

export function policyFor(url: URL): CachePolicy | null {
  const path = url.pathname.slice(base.length);
  const policy = readable.find((candidate) => candidate.pattern.test(path))?.policy ?? null;

  // Without `v` the picture is mutable; leave it to the browser's ETag revalidation.
  if (policy === 'cache-first' && !url.searchParams.has('v')) {
    return null;
  }

  return policy;
}

/**
 * Answers a read and keeps a copy for offline use; cached responses keep their headers (ETag).
 * Must never throw: a rejection reaches the page as "Failed to fetch".
 */
export async function apiResponse(event: FetchEvent, policy: CachePolicy): Promise<Response> {
  const request = event.request;
  const cache = await caches.open(privateCacheName).catch(() => null);
  const [owner, kept] = cache
    ? await Promise.all([signedInAs(cache), cache.match(request).catch(() => undefined)])
    : [null, undefined];
  const cached = owner && kept?.headers.get(ownerHeader) === owner ? kept : undefined;

  if (cached && policy === 'cache-first') {
    return cached;
  }

  // A `Request` is spent by its fetch, so start once. Kept alive past the answer so a slow network still refreshes the copy.
  const fresh = fetchAndStore(request, cache, owner);

  event.waitUntil(fresh);

  const response = cached ? await Promise.race([fresh, deadline()]) : await fresh;

  if (response) {
    return response;
  }

  if (cached) {
    return cached;
  }

  // Nothing cached and no network: re-throw the ordinary fetch failure.
  return fetch(request.url, { credentials: 'include', headers: request.headers });
}

/** Resolves with null once the network has had its chance. */
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

    // A session that ended while the app was closed never reached a sign-out; the boot 401 is the only signal.
    if (response.status === 401) {
      await caches.delete(privateCacheName).catch(() => false);
      await forgetKitchen();

      return response;
    }

    // A 304 has no body and a cached error outlives its fix. A refresh landing after
    // `culina:forget` writes into the already-deleted cache, so it cannot resurrect copies.
    if (cache && response.ok && response.type === 'basic') {
      try {
        const readFor =
          new URL(request.url).pathname === whoIsSignedIn ? await userIdIn(response) : owner;

        if (readFor) {
          await cache.put(request, ownedCopy(response, readFor));
          await trim(cache);
        }
      } catch {
        // Out of quota or an unstorable response; not worth failing over.
      }
    }

    return response;
  } catch {
    return null;
  }
}

/** The user the kept `whoIsSignedIn` names, or null. */
async function signedInAs(cache: Cache): Promise<string | null> {
  const me = await cache.match(whoIsSignedIn).catch(() => undefined);

  return me?.headers.get(ownerHeader) ?? null;
}

async function userIdIn(response: Response): Promise<string | null> {
  const { userId } = (await response.clone().json()) as { userId?: unknown };

  return typeof userId === 'string' ? userId : null;
}

/** The response plus its owner header. */
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
