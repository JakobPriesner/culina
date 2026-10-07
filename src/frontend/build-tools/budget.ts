import { gzipSync } from 'node:zlib';
import { glob, readFile } from 'node:fs/promises';

/** Gzipped size budgets, measured on every run; the first load is what someone waits for on a bad signal. */

/**
 * Raise a limit deliberately, in a commit that says what was added and why, never to make a build pass.
 * `firstLoadBytes` is the number to defend; the totals only bound the worst navigation.
 */
export const budgets = {
  firstLoadBytes: 80 * 1024,
  totalJavaScriptBytes: 500 * 1024,
  totalStyleBytes: 60 * 1024
} as const;

export interface Weight {
  readonly firstLoad: number;
  readonly javaScript: number;
  readonly styles: number;
}

const gzipped = (bytes: Buffer | string) => gzipSync(bytes, { level: 9 }).byteLength;

export async function weigh(buildDir: string): Promise<Weight> {
  const html = await readFile(`${buildDir}/index.html`, 'utf8');

  // What the shell references directly: entry scripts and the stylesheets needed to paint.
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
