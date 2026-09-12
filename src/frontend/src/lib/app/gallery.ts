/**
 * Whether the design-system gallery exists in this build.
 *
 * Always while developing. In a built app only when `VITE_GALLERY=1` was set,
 * which the end-to-end runner does and a release does not — so the gallery can
 * be driven by a real browser (focus trapping and the top layer are browser
 * behaviour that jsdom cannot show) without shipping a route that looks like a
 * feature.
 *
 * `__CULINA_GALLERY__` rather than `import.meta.env.VITE_GALLERY`, and that
 * distinction is the whole point. Vite only substitutes a `VITE_` variable it
 * has a value for; with the variable unset — which is exactly the release case
 * — the expression survives as a runtime read of an object, nothing behind it
 * can be removed, and the entire gallery ships. It did. `define` always emits a
 * literal, so `galleryEnabled` is statically `false` in a release build and the
 * bundler deletes every specimen behind it.
 *
 * `build-tools/assertNoGallery.ts` proves it, on every release build.
 */
export const galleryEnabled = import.meta.env.DEV || __CULINA_GALLERY__;
