import { gzipSync } from 'node:zlib';
import { glob, readFile } from 'node:fs/promises';

/**
 * What the app is allowed to weigh.
 *
 * A recipe app is read on a phone in a kitchen, often on whatever signal
 * reaches the back of a house. The number that matters is the first load —
 * everything the document asks for before anything is on screen — and the way
 * it gets worse is never a decision anybody made: it is one convenient
 * dependency, pulled in for one helper, that brings a date library with it.
 *
 * Gzipped, because that is what is actually transferred. Measured rather than
 * estimated, and printed on every run so the number is visible before it is a
 * failure.
 */

/**
 * The limits. Raise one deliberately, in a commit that says what was added and
 * why it was worth it — never to make a build pass.
 *
 * The two totals were re-baselined on 21 September 2026, against an app that
 * had roughly half again as much in it as the one they were written for: 21
 * pages rather than 13, 128 components rather than 92, and 808 translated
 * strings rather than 297. Measured rather than assumed — no dependency had
 * crept in, the only runtime one is still openapi-fetch, and the largest
 * chunks are the Svelte and SvelteKit runtimes. Building with one locale
 * instead of two gives 205.5 kB, so the second language is 16.9 kB of it:
 * Paraglide inlines both strings and a dispatcher for every message.
 *
 * `firstLoadBytes` did not move and should be the last one that ever does. It
 * is what somebody waits for at the back of a house on a bad signal, and at
 * 70.3 kB there is still room under it.
 *
 * The totals moved again on 24 September 2026, for the server's own setup:
 * the first-run screen and Settings → Server, two pages that one administrator
 * opens a handful of times in the life of an instance. Measured against the
 * build before them, they are 14.7 kB of script — 5.5 kB of it the 85 new
 * strings in both languages, 4.2 kB the two pages, 4.3 kB the fields they
 * share — and 1.0 kB of styles, with no new dependency. The search highlight
 * just before them had already taken the last of the old headroom (236.9 kB).
 * Worth it: without them a fresh container did not start at all, and the
 * settings could only be changed by somebody with a shell. Neither page is on
 * the first load, which went from 70.6 to 71.0 kB.
 */
export const budgets = {
  /** Everything the shell asks for before it can render: scripts and styles. */
  firstLoadBytes: 80 * 1024,
  /** Every chunk of every route together, which bounds the worst navigation. */
  totalJavaScriptBytes: 260 * 1024,
  totalStyleBytes: 40 * 1024
} as const;

export interface Weight {
  readonly firstLoad: number;
  readonly javaScript: number;
  readonly styles: number;
}

const gzipped = (bytes: Buffer | string) => gzipSync(bytes, { level: 9 }).byteLength;

export async function weigh(buildDir: string): Promise<Weight> {
  const html = await readFile(`${buildDir}/index.html`, 'utf8');

  // What the shell references directly: the entry scripts and the stylesheets
  // the page cannot paint without.
  const referenced = [...html.matchAll(/(?:href|src)="(\/_app\/[^"]+)"/g)].map(
    (match) => match[1]!
  );

  let firstLoad = gzipped(html);

  for (const reference of new Set(referenced)) {
    firstLoad += gzipped(await readFile(`${buildDir}${reference}`));
  }

  let javaScript = 0;
  let styles = 0;

  for await (const entry of glob('_app/**/*.{js,css}', { cwd: buildDir })) {
    const size = gzipped(await readFile(`${buildDir}/${entry}`));

    if (entry.endsWith('.css')) {
      styles += size;
    } else {
      javaScript += size;
    }
  }

  return { firstLoad, javaScript, styles };
}

export function over(weight: Weight): string[] {
  const checks = [
    ['first load', weight.firstLoad, budgets.firstLoadBytes],
    ['all JavaScript', weight.javaScript, budgets.totalJavaScriptBytes],
    ['all styles', weight.styles, budgets.totalStyleBytes]
  ] as const;

  return checks
    .filter(([, actual, limit]) => actual > limit)
    .map(([name, actual, limit]) => `${name}: ${kb(actual)} over a budget of ${kb(limit)}`);
}

export const describe = (weight: Weight): string =>
  `first load ${kb(weight.firstLoad)} · all JavaScript ${kb(weight.javaScript)} · ` +
  `all styles ${kb(weight.styles)} (gzipped)`;

const kb = (bytes: number) => `${(bytes / 1024).toFixed(1)} kB`;
