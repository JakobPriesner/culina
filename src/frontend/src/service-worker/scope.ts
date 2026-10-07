import { base, version } from '$service-worker';

export const worker = self as unknown as ServiceWorkerGlobalScope;

/** One cache per build, so a half-updated cache (new HTML, old JS) can't happen; the old one goes once the new worker takes over. */
export const cacheName = `culina-${version}`;

/** Read recipes, deliberately unversioned so a deploy doesn't drop recipes people rely on offline. */
export const privateCacheName = 'culina-private';

/** The shell document every route renders into: the root path, which is what navigations request, not `/index.html`. */
export const shellDocument = `${base}/`;
