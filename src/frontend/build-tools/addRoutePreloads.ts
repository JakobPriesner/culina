import { addPreloads } from './routePreloads.ts';

/** The route-preload step as a command, part of `pnpm build` and run before compression; prints what it added to every shell. */
const { added } = await addPreloads(process.argv[2] ?? 'build');

console.log(`route preloads: ${added} bytes added to index.html`);
