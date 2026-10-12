import { apiUrl } from './instance';
import { native } from './platform';
import type { OfflineReads } from './offlineReads';

/**
 * The app's one way out to its server. The website's reads go through the service worker, which keeps the offline copies;
 * the app has none, so the same reads are kept here, from the page. On the website this is plain `fetch`, and the
 * offline-reads module is never loaded (it is imported only behind `native`).
 */
let reads: Promise<OfflineReads> | null = null;

function offlineReads(): Promise<OfflineReads> {
  reads ??= import('./offlineReads').then((module) =>
    module.offlineReads({
      base: '',
      locate: apiUrl,
      // The page's own 401 handling ends the session and forgets the kitchen (`forgetCachedReads`).
      signedOut: () => Promise.resolve()
    })
  );

  return reads;
}

export async function send(request: Request): Promise<Response> {
  if (!native || request.method !== 'GET') {
    return fetch(request);
  }

  const kept = await offlineReads();
  const policy = kept.policyFor(new URL(request.url));

  return policy ? kept.answer(request, policy, () => {}) : fetch(request);
}

/** Empties the app's offline copies on sign-in and sign-out, as `culina:forget` does for the worker's. */
export async function forgetOfflineReads(): Promise<void> {
  if (native && typeof caches !== 'undefined') {
    const { privateCacheName } = await import('./offlineReads');

    await caches.delete(privateCacheName).catch(() => false);
  }
}
