import { findGalleryText, galleryRemedy } from './assertNoGallery.ts';

/**
 * The release check, as a command: `pnpm verify:release`.
 *
 * Run after a plain `pnpm build` — CI does exactly that — because the thing it
 * looks at is the build output, and the only build worth checking is one made
 * without the gallery flag.
 */
const buildDir = process.argv[2] ?? 'build';
const found = await findGalleryText(buildDir);

if (found.length > 0) {
  console.error(`The design-system gallery is in ${buildDir}:\n`);

  for (const line of found) {
    console.error(`  ${line}`);
  }

  console.error(`\n${galleryRemedy}`);
  process.exit(1);
}

console.log(`No gallery in ${buildDir}.`);
