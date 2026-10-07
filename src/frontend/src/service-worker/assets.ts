import { base, build, files } from '$service-worker';
import { cacheName, shellDocument } from './scope';

/**
 * The only `static/` files installed up front: `files` is ~1.2 MB of photos and large icons most devices never use.
 * `assetResponse` still caches anything actually fetched.
 */
const linkedByTheDocument = new Set([
  `${base}/favicon.ico`,
  `${base}/icon.svg`,
  `${base}/manifest.webmanifest`
]);

const shell = [...build, ...files.filter((file) => linkedByTheDocument.has(file))];

const hashedBuildFiles = new Set(build);

/** The shell document must cache or the install fails; the rest is best effort, since `addAll` would fail wholly on one 404. */
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

/** Cache first for hashed build files (immutable), network first for static files that keep their name across builds. */
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

    // Skip opaque and error responses: a cached 404 outlives the deploy that fixes it.
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
