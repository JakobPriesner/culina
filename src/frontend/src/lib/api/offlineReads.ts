/*
 * The narrow API exception: exact reads kept so opened recipes stay readable offline. Writes are never answered from here.
 * Shared by the website's service worker (src/service-worker/apiCache.ts) and the installed app, which has no worker
 * (WKWebView offers none on its own scheme) and keeps the same copies from the page (./transport.ts).
 */

/** The cache both keep their copies in: deliberately unversioned, so a deploy does not drop recipes people rely on offline. */
export const privateCacheName = 'culina-private';

const privateCacheLimit = 120;

/**
 * Names the user a kept copy was read for; a copy is only answered to that user, never an unowned one.
 * Needed because the cache is keyed by address alone and the sign-out message may never arrive.
 */
const ownerHeader = 'X-Culina-Owner';

/** How long a network-first read waits when a copy exists; a stalled connection never rejects. Under the app's 4s boot wait. */
const networkDeadlineMs = 2_500;

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

export interface OfflineReadsOptions {
  /** The path prefix the API lives under (SvelteKit's `base`). */
  readonly base: string;
  /** Where an API path is fetched: itself for the worker, the connected server for the app. */
  readonly locate: (path: string) => string;
  /** Runs when a read answers 401: a session that ended while the app was closed never reached a sign-out. */
  readonly signedOut: () => Promise<void>;
}

export interface OfflineReads {
  isApi(url: URL): boolean;
  policyFor(url: URL): CachePolicy | null;
  /**
   * Answers a read and keeps a copy for offline use; cached responses keep their headers (ETag). `keepAlive` lets the
   * refresh outlive the answer (a worker's `waitUntil`). Never throws: a rejection reaches the page as "Failed to fetch".
   */
  answer(
    request: Request,
    policy: CachePolicy,
    keepAlive: (work: Promise<unknown>) => void
  ): Promise<Response>;
}

export function offlineReads({ base, locate, signedOut }: OfflineReadsOptions): OfflineReads {
  const whoIsSignedInPath = `${base}/api/v1/users/me`;
  const whoIsSignedIn = locate(whoIsSignedInPath);

  const isApi = (url: URL): boolean =>
    url.pathname === `${base}/api` || url.pathname.startsWith(`${base}/api/`);

  function policyFor(url: URL): CachePolicy | null {
    const path = url.pathname.slice(base.length);
    const policy = readable.find((candidate) => candidate.pattern.test(path))?.policy ?? null;

    // Without `v` the picture is mutable; leave it to the browser's ETag revalidation.
    if (policy === 'cache-first' && !url.searchParams.has('v')) {
      return null;
    }

    return policy;
  }

  async function answer(
    request: Request,
    policy: CachePolicy,
    keepAlive: (work: Promise<unknown>) => void
  ): Promise<Response> {
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

    keepAlive(fresh);

    const response = cached ? await firstOf(fresh) : await fresh;

    // A gateway error is the server being away behind its proxy, which for reading is the same as no network: the
    // kept copy beats "Culina is not answering" (an app started on a train, a kitchen server rebooting).
    if (response && !(cached && unreachable(response))) {
      return response;
    }

    if (cached) {
      return cached;
    }

    // Nothing cached and no network: re-throw the ordinary fetch failure.
    return fetch(request.url, { credentials: request.credentials, headers: request.headers });
  }

  /** Whether an answer came from a proxy whose server is down, not from Culina. */
  function unreachable(response: Response): boolean {
    return response.status === 502 || response.status === 503 || response.status === 504;
  }

  /** Fetches, stores what is worth storing for `owner`, and never rejects. */
  async function fetchAndStore(
    request: Request,
    cache: Cache | null,
    owner: string | null
  ): Promise<Response | null> {
    try {
      const response = await fetch(request);

      if (response.status === 401) {
        await caches.delete(privateCacheName).catch(() => false);
        await signedOut();

        return response;
      }

      // A 304 has no body and a cached error outlives its fix. A refresh landing after a sign-out
      // writes into the already-deleted cache, so it cannot resurrect copies. `cors` is the app's
      // answer from its server; the worker only ever sees `basic`.
      if (cache && response.ok && (response.type === 'basic' || response.type === 'cors')) {
        try {
          const readFor =
            new URL(request.url).pathname === whoIsSignedInPath ? await userIdIn(response) : owner;

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

  return { isApi, policyFor, answer };
}

/** The network's answer, or null once it has had its chance; the timer is cleared as soon as either settles. */
async function firstOf(fresh: Promise<Response | null>): Promise<Response | null> {
  let timer: ReturnType<typeof setTimeout> | undefined;
  const deadline = new Promise<null>((resolve) => {
    timer = setTimeout(() => resolve(null), networkDeadlineMs);
  });

  try {
    return await Promise.race([fresh, deadline]);
  } finally {
    clearTimeout(timer);
  }
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
