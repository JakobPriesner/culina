import { describe, precompress } from './precompress.ts';

/**
 * The compression step, as a command. Part of `pnpm build`, so every build
 * that ships — CI's, the image's — has its copies.
 *
 * Prints the totals every run, the same way the budget does, so the number
 * that is actually sent is in the log next to the number the budget measures.
 */
const buildDir = process.argv[2] ?? 'build';

console.log(describe(await precompress(buildDir)));
