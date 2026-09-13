import { describe, over, weigh } from './budget.ts';

/**
 * The weight check, as a command: `pnpm verify:budget`.
 *
 * Run after a build. Prints what the app weighs whether or not it passes, so
 * the number is in the log of every CI run rather than only in the one that
 * failed.
 */
const buildDir = process.argv[2] ?? 'build';
const weight = await weigh(buildDir);

console.log(describe(weight));

const exceeded = over(weight);

if (exceeded.length > 0) {
  console.error('\nOver budget:\n');

  for (const line of exceeded) {
    console.error(`  ${line}`);
  }

  console.error(
    '\nRaise a budget in build-tools/budget.ts deliberately, in a commit that ' +
      'says what was added and why it was worth it — never to make a build pass.'
  );

  process.exit(1);
}
