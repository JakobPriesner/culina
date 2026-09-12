/**
 * What `$service-worker` gives the worker, declared here rather than taken
 * from SvelteKit's ambient types.
 *
 * Those types describe the app, and reach for the DOM to do it; a worker has no
 * DOM, and a `lib` that contains both ends up with two of every global. This is
 * the whole of what the worker actually imports.
 */
declare module '$service-worker' {
  /** The path the app is served under. Empty at the root of an origin. */
  export const base: string;
  /** The build's own files, every one of them content-hashed. */
  export const build: string[];
  /** Everything in `static/`, served under its own name. */
  export const files: string[];
  /** Pages rendered at build time. */
  export const prerendered: string[];
  /** Changes on every build. */
  export const version: string;
}
