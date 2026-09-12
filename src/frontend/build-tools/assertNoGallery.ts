import { glob, readFile } from 'node:fs/promises';

/**
 * Fails if a release build contains the design-system gallery.
 *
 * This has gone wrong twice, both times silently. First the flag was read with
 * a bracket lookup, which Vite does not substitute, so the condition stayed a
 * runtime read and the entire gallery shipped. Then the flag became a real
 * constant and the gallery *still* shipped, because a Svelte component's
 * templates are hoisted to module scope and a bundler will not delete them for
 * an `{#if}` it can prove is false.
 *
 * Neither was visible in review, and the end-to-end suite cannot notice: it
 * builds with the gallery deliberately switched on. So the build is searched
 * for text that exists nowhere else.
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
