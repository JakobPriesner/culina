import { findGalleryText, galleryRemedy } from './assertNoGallery.ts';

/** The release check, as `pnpm verify:release`: run after a plain `pnpm build` (as CI does), since only a build without the gallery flag is worth checking. */
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
