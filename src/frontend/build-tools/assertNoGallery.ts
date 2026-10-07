import { glob, readFile } from 'node:fs/promises';

/**
 * Fails if a release build contains the design-system gallery. It shipped twice silently: a bracket-lookup flag Vite does not substitute, then a constant flag whose `{#if}` a bundler cannot delete because Svelte hoists templates to module scope.
 * Neither shows in review and e2e builds with the gallery on, so the build is searched for text that exists nowhere else.
 */

/** Specimen text, each of it inside the gallery and nowhere near the product. */
const galleryOnly = [
  'Overlays and containers',
  'A distinct entity',
  'Roughly 320 kcal a serving',
  'A sheet on a phone, a dialog on a desktop'
];

export async function findGalleryText(buildDir: string): Promise<string[]> {
  const found: string[] = [];

  for await (const entry of glob('**/*.{js,css,html}', { cwd: buildDir })) {
    const contents = await readFile(`${buildDir}/${entry}`, 'utf8');

    for (const specimen of galleryOnly) {
      if (contents.includes(specimen)) {
        found.push(`${entry} contains "${specimen}"`);
      }
    }
  }

  return found.sort();
}

export const galleryRemedy =
  'The design-system gallery is in the release build. It is built in only when ' +
  'VITE_GALLERY=1, which the end-to-end runner sets and a release must not: ' +
  'check build-tools/gallery.ts and src/lib/app/gallery.ts.';
