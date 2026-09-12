import type { Plugin } from 'vite';

/**
 * Keeps the design-system gallery out of a release build.
 *
 * The gallery exists to exercise components a browser has to run — focus
 * trapping, the top layer, light dismiss — and it is not part of the product.
 * Being unreachable is not the same as being absent: the route already answers
 * 404, but the specimen markup was still in the bundle, because a Svelte
 * component's templates are hoisted to module scope and a bundler will not
 * remove them for an `{#if}` it can prove is false.
 *
 * So the components are replaced with an empty one before they are ever
 * compiled. What ships is a route that renders nothing and 404s — and no
 * specimen text at all.
 *
 * `enforce: 'pre'` matters: this has to answer before the Svelte plugin reads
 * the real file from disk.
 */
const gallerySources = [
  'src/routes/design/Gallery.svelte',
  'src/lib/features/recipes/preview/RecipeExperience.svelte'
];

/** A component that renders nothing, in the form the Svelte plugin expects. */
const nothing = '<script lang="ts"></script>\n';

export function galleryOnly(enabled: boolean): Plugin {
  return {
    name: 'culina:gallery-only',
    enforce: 'pre',
    apply: 'build',

    load(id) {
      const path = id.split('?')[0]?.replaceAll('\\', '/') ?? '';

      if (enabled || !gallerySources.some((source) => path.endsWith(source))) {
        return null;
      }

      return nothing;
    }
  };
}
