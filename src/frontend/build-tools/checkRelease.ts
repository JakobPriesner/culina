import { findNativeText, findServiceWorker, findSourceMaps } from './assertReleaseContents.ts';
import { findGalleryText, galleryRemedy } from './assertNoGallery.ts';

/**
 * The release check, as `pnpm verify:release`: run after a plain `pnpm build` (as CI does), since only a build without
 * the gallery flag is worth checking. `pnpm verify:release build-native` checks the app's bundle (`pnpm build:native`).
 */
const buildDir = process.argv[2] ?? 'build';
const app = buildDir.endsWith('build-native');

const problems: { found: string[]; remedy: string }[] = [
  { found: await findGalleryText(buildDir), remedy: galleryRemedy },
  {
    found: await findSourceMaps(buildDir),
    remedy: 'Source maps are never shipped (CLAUDE.md): check build.sourcemap in vite.config.ts.'
  },
  app
    ? {
        found: await findServiceWorker(buildDir),
        remedy: 'The app bundle has no service worker: build-tools/finishNativeShell.ts removes it.'
      }
    : {
        found: await findNativeText(buildDir),
        remedy:
          "The app's code is in the website. __CULINA_NATIVE__ must be a literal false here (vite.config.ts), and native code must sit behind it (src/lib/api/platform.ts)."
      }
];

let failed = false;

for (const { found, remedy } of problems) {
  if (found.length === 0) {
    continue;
  }

  failed = true;
  console.error(`${buildDir} is not releasable:\n`);

  for (const line of found) {
    console.error(`  ${line}`);
  }

  console.error(`\n${remedy}\n`);
}

if (failed) {
  process.exit(1);
}

console.log(
  `${buildDir}: no gallery, no source maps, ${app ? 'no service worker' : 'no app code'}.`
);
