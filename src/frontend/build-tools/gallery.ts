import type { Plugin } from 'vite';

/**
 * Keeps the design-system gallery out of a release build: the route 404s but hoisted template markup still shipped,
 * so the components are replaced with an empty one before compiling. `enforce: 'pre'` so this runs before the Svelte plugin reads the file.
 */
const gallerySources = [
  'src/routes/design/Gallery.svelte',
  'src/lib/features/recipes/preview/RecipeExperience.svelte'
];

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
