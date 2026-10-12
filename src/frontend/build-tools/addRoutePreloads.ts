import { addPreloads } from './routePreloads.ts';

/** The route-preload step as a command, part of `pnpm build` and run before compression; prints what it added to every shell. */
const buildDir = process.argv[2] ?? 'build';

// The app's shell preloads code only (see `addPreloads`).
const { added } = await addPreloads(buildDir, '.svelte-kit', {
  data: !buildDir.endsWith('build-native')
});

console.log(`route preloads: ${added} bytes added to index.html`);
