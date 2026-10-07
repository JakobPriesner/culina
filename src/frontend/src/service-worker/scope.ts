import { base, version } from '$service-worker';

export const worker = self as unknown as ServiceWorkerGlobalScope;

/**
 * One cache per build.
 *
 * `version` changes on every build, so a new worker fills a new cache and the
 * old one is deleted only once the new worker takes over. A half-updated cache
 * — new HTML, old JavaScript — is the failure mode that makes people uninstall
 * a PWA, and a cache per version makes it impossible.
 */
export const cacheName = `culina-${version}`;

/**
 * Recipes that have been read, kept apart from the build's own files.
 *
 * Not named for the version: a deploy must not cost somebody the recipes they
 * are relying on being able to open. Its lifetime is a session, not a build.
 */
export const privateCacheName = 'culina-private';

/**
 * The document every route renders into.
 *
 * The root path, not `/index.html`: that is what a navigation actually asks
 * for, and the host renders the shell there rather than serving a file — the
 * file name is an implementation detail of one particular way of hosting it.
 */
export const shellDocument = `${base}/`;
