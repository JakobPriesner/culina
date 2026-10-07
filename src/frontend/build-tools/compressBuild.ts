import { describe, precompress } from './precompress.ts';

/** The compression step as a command, part of `pnpm build`; prints totals every run, next to what the budget measures. */
const buildDir = process.argv[2] ?? 'build';

console.log(describe(await precompress(buildDir)));
