import { base, build, files } from '$service-worker';
import { cacheName, shellDocument } from './scope';

/**
 * The static files the document itself links to, and the only ones installed.
 *
 * Named rather than taken from `files` wholesale. `files` is everything in
 * `static/`, and that was 1.2 MB: three 1536-pixel photographs for the sign-in
 * and preview pages, and the 512-pixel icons the operating system reads once,
 * when somebody adds the app to a home screen. A signed-in person on their own
 * phone sees none of them and was downloading all of them on the first visit
 * and again after every deploy.
 *
 * Nothing is lost offline by leaving them out: `assetResponse` keeps every
 * static file it fetches, so anything that has actually been on screen is
 * still there without a network. And a list is the safer default than a rule
 * about what to skip — a new file in `static/` is not installed on every
 * device until somebody decides it should be.
 */
const linkedByTheDocument = new Set([
  `${base}/favicon.ico`,
  `${base}/icon.svg`,
  `${base}/manifest.webmanifest`
]);

/**
 * Everything else worth having before the network goes away.
 *
 * Storing the document also stores the CSP header that came with it, so the
 * nonce inside the cached shell and the nonce its policy names are the same
 * one — which is the whole reason the shell is rendered rather than served
 * from disk.
 */
const shell = [...build, ...files.filter((file) => linkedByTheDocument.has(file))];

/** The build's own files, asked about on every request. */
const hashedBuildFiles = new Set(build);

/**
 * Fills the cache for this build.
 *
 * The document has to be there — without it the app does not open offline at
 * all, and an install that cannot promise that is worth failing. Everything
 * else is best effort: `addAll` is all or nothing, so one file that 404s
 * because a deploy moved it would mean no offline app whatsoever, which is a
 * great deal worse than one uncached icon.
 */
export async function precache(): Promise<void> {
  const cache = await caches.open(cacheName);

  await cache.add(shellDocument);
  await Promise.allSettled(shell.map((asset) => cache.add(asset)));
}

export async function shellResponse(): Promise<Response> {
  const cache = await caches.open(cacheName);
  const cached = await cache.match(shellDocument);

  return cached ?? fetch(shellDocument);
}

/**
 * Cache first for the build's own assets, network first for the rest.
 *
 * A file under `build` has a hash in its name: its contents cannot change, so
 * asking the network about it is a round trip that can only ever return the
 * same bytes. A static file — an icon, the manifest — keeps its name across
 * builds, so the network's copy is the newer one when there is a network.
 */
export async function assetResponse(event: FetchEvent, url: URL): Promise<Response> {
  const request = event.request;
  const cache = await caches.open(cacheName);

  if (hashedBuildFiles.has(url.pathname)) {
    const cached = await cache.match(request);

    if (cached) {
      return cached;
    }
  }

  try {
    const response = await fetch(request);

    // Opaque and error responses are not worth keeping: a cached 404
    // outlives the deploy that fixes it. Kept after the answer has gone, not
    // before it.
    if (response.ok && response.type === 'basic') {
      event.waitUntil(cache.put(request, response.clone()).catch(() => undefined));
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
