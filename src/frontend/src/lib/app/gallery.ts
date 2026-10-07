/**
 * Whether the design-system gallery exists in this build: always in dev, in a release only with `VITE_GALLERY=1` (e2e runner).
 * `__CULINA_GALLERY__` is a `define` literal, not `import.meta.env.VITE_GALLERY`: an unset VITE_ var survives as a runtime read,
 * so the gallery could not be removed and shipped. `build-tools/assertNoGallery.ts` proves it on every release.
 */
export const galleryEnabled = import.meta.env.DEV || __CULINA_GALLERY__;
