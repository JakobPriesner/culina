/**
 * Whether the design-system gallery exists in this build.
 *
 * Always while developing. In a built app only when `VITE_GALLERY` was set,
 * which the end-to-end runner does and a release does not — so the gallery can
 * be driven by a real browser (focus trapping and the top layer are browser
 * behaviour that jsdom cannot show) without shipping a route that looks like a
 * feature.
 *
 * Both values are replaced at build time — which is why this is written with
 * dot notation: Vite substitutes `import.meta.env.NAME` in the source text and
 * only that form, so a bracket lookup would survive as a runtime read and
 * nothing depending on it could be removed.
 */
export const galleryEnabled = import.meta.env.DEV || import.meta.env.VITE_GALLERY === '1';
