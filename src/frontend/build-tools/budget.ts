import { gzipSync } from 'node:zlib';
import { glob, readFile } from 'node:fs/promises';

import { readKit, routeFiles } from './routePreloads.ts';
import type { Dictionary, Manifest } from './routePreloads.ts';

/** Gzipped size budgets, measured on every run; the first load is what someone waits for on a bad signal. */

/**
 * Raise a limit deliberately, in a commit that says what was added and why, never to make a build pass.
 * `firstLoadBytes` is the shell alone; `heaviestRouteBytes` is what a cold visit to the heaviest route downloads in all
 * (the shell, its entry files and the route's own modules and styles), the number someone on a bad signal waits for.
 * The totals only bound the worst navigation.
 */
export const budgets = {
  firstLoadBytes: 80 * 1024,
  heaviestRouteBytes: 270 * 1024,
  totalJavaScriptBytes: 500 * 1024,
  totalStyleBytes: 60 * 1024
} as const;

export interface Weight {
  readonly firstLoad: number;
  /** Cold-load bytes per route, heaviest first. */
  readonly routes: readonly RouteWeight[];
  readonly javaScript: number;
  readonly styles: number;
}

export interface RouteWeight {
  readonly route: string;
  readonly bytes: number;
}

const gzipped = (bytes: Buffer | string) => gzipSync(bytes, { level: 9 }).byteLength;

/** What a cold visit to each route downloads: the shell and its referenced files, plus the route's modules and styles, each file once. */
export function coldLoads(
  manifest: Manifest,
  dictionary: Dictionary,
  shell: { readonly bytes: number; readonly files: Iterable<string> },
  sizeOf: (file: string) => number
): RouteWeight[] {
  return Object.entries(routeFiles(manifest, dictionary))
    .map(([route, files]) => ({
      route,
      bytes:
        shell.bytes +
        [...new Set([...shell.files, ...files])].reduce((sum, file) => sum + sizeOf(file), 0)
    }))
    .sort((a, b) => b.bytes - a.bytes);
}

export async function weigh(buildDir: string, kitDir = '.svelte-kit'): Promise<Weight> {
  const html = await readFile(`${buildDir}/index.html`, 'utf8');

  let javaScript = 0;
  let styles = 0;
  const sizes = new Map<string, number>();

  for await (const entry of glob('_app/**/*.{js,css}', { cwd: buildDir })) {
    const size = gzipped(await readFile(`${buildDir}/${entry}`));

    sizes.set(entry, size);

    if (entry.endsWith('.css')) {
      styles += size;
    } else {
      javaScript += size;
    }
  }

  // What the shell references directly: entry scripts and the stylesheets needed to paint.
  const referenced = new Set(
    [...html.matchAll(/(?:href|src)="\/(_app\/[^"]+)"/g)].map((match) => match[1]!)
  );
  const shell = { bytes: gzipped(html), files: referenced };
  const sizeOf = (file: string) => sizes.get(file) ?? 0;
  const { manifest, dictionary } = await readKit(kitDir);

  return {
    firstLoad: shell.bytes + [...referenced].reduce((sum, file) => sum + sizeOf(file), 0),
    routes: coldLoads(manifest, dictionary, shell, sizeOf),
    javaScript,
    styles
  };
}

export function over(weight: Weight): string[] {
  const checks = [
    ['first load', weight.firstLoad, budgets.firstLoadBytes],
    ['heaviest route', weight.routes[0]?.bytes ?? 0, budgets.heaviestRouteBytes],
    ['all JavaScript', weight.javaScript, budgets.totalJavaScriptBytes],
    ['all styles', weight.styles, budgets.totalStyleBytes]
  ] as const;

  return checks
    .filter(([, actual, limit]) => actual > limit)
    .map(([name, actual, limit]) => `${name}: ${kb(actual)} over a budget of ${kb(limit)}`);
}

export const describe = (weight: Weight): string =>
  `first load ${kb(weight.firstLoad)} · all JavaScript ${kb(weight.javaScript)} · ` +
  `all styles ${kb(weight.styles)} (gzipped)\n` +
  `heaviest cold loads: ${weight.routes
    .slice(0, 5)
    .map(({ route, bytes }) => `${route} ${kb(bytes)}`)
    .join(' · ')}`;

const kb = (bytes: number) => `${(bytes / 1024).toFixed(1)} kB`;
