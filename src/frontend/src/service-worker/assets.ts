import { immutable } from '$app/manifest';
import { asset } from '$app/paths';
import { cacheName, shellDocument } from './scope';

/** The build's own files, every one content-hashed; the manifest lists them relative to the root the app is served from. */
const hashedBuildFiles = new Set(immutable.map(({ path }) => `/${path}`));

/**
 * The only `static/` files installed up front: the rest is ~1.2 MB of photos and large icons most devices never use.
 * `assetResponse` still caches anything actually fetched.
 */
const linkedByTheDocument = [
  asset('favicon.ico'),
  asset('icon.svg'),
  asset('manifest.webmanifest')
];

// Every build file but the barcode decoder's WebAssembly: 1.1 MB that only a browser without a decoder of its own fetches.
const shell = [...hashedBuildFiles]
  .filter((file) => !file.endsWith('.wasm'))
  .concat(linkedByTheDocument);

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
    // Installation and module/style requests can send different Origin/Accept headers. A proxy's Vary
    // must not hide the installed copy: the content hash fixes these files' bytes for every request.
    const cached = await cache.match(request, { ignoreVary: true });

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
