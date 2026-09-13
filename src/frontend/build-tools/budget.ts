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
 */
export const budgets = {
  /** Everything the shell asks for before it can render: scripts and styles. */
  firstLoadBytes: 80 * 1024,
  /** Every chunk of every route together, which bounds the worst navigation. */
  totalJavaScriptBytes: 140 * 1024,
  totalStyleBytes: 24 * 1024
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
