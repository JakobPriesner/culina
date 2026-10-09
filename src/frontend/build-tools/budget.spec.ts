import { describe, expect, it } from 'vitest';

import { coldLoads, describe as describeWeight, over } from './budget';
import { parseDictionary } from './routePreloads';
import type { Manifest } from './routePreloads';

/* A route's cold load is the shell plus its nodes' static imports and styles, each file counted once. */
const file = (name: string) => `_app/immutable/${name}`;
const node = (id: number) => `.svelte-kit/generated/client-optimized/nodes/${id}.js`;

const manifest: Manifest = {
  [node(0)]: { file: file('nodes/0.js'), imports: ['_shell.js'], css: [file('assets/0.css')] },
  [node(2)]: { file: file('nodes/2.js'), imports: ['_big.js'] },
  [node(4)]: { file: file('nodes/4.js') },
  [node(6)]: { file: file('nodes/6.js'), imports: ['_big.js'], css: [file('assets/6.css')] },
  [node(8)]: { file: file('nodes/8.js'), imports: ['_big.js'] },
  '_shell.js': { file: file('chunks/shell.js') },
  '_big.js': { file: file('chunks/big.js'), css: [file('assets/big.css')] }
};

const dictionary = parseDictionary(`{
  "/(app)": [6,[2]],
  "/(auth)/login": [4,[]],
  "/(app)/recipes/[id]": [8,[2]]
}`);

const sizes: Record<string, number> = {
  [file('nodes/0.js')]: 10,
  [file('assets/0.css')]: 5,
  [file('chunks/shell.js')]: 100,
  [file('nodes/2.js')]: 20,
  [file('nodes/4.js')]: 1,
  [file('nodes/6.js')]: 30,
  [file('nodes/8.js')]: 40,
  [file('assets/6.css')]: 7,
  [file('chunks/big.js')]: 200,
  [file('assets/big.css')]: 3
};

// The shell's own bytes, and the two files it references.
const shell = { bytes: 50, files: [file('nodes/0.js'), file('chunks/shell.js')] };
const loads = coldLoads(manifest, dictionary, shell, (name) => sizes[name] ?? 0);
const bytesOf = (route: string) => loads.find((load) => load.route === route)?.bytes;

describe('the cold load of a route', () => {
  it('is the shell, then the route modules and the stylesheets they bring', () => {
    // 50 + shell files 110 + node 0 css 5 + node 4 (1)
    expect(bytesOf('/(auth)/login')).toBe(166);
  });

  it('counts a chunk shared by a layout and its page once', () => {
    // 50 + 110 + 5 + node 2 (20) + big (200 + 3) + node 6 (30) + 6.css (7)
    expect(bytesOf('/(app)')).toBe(425);
  });

  it('lists the heaviest route first', () => {
    expect(loads.map((load) => load.route)).toEqual([
      '/(app)/recipes/[id]',
      '/(app)',
      '/(auth)/login'
    ]);
    expect(bytesOf('/(app)/recipes/[id]')).toBe(428);
  });
});

describe('the budget', () => {
  const weight = {
    firstLoad: 1,
    routes: [{ route: '/(app)', bytes: 1e6 }],
    javaScript: 1,
    styles: 1
  };

  it('names a route set that is too heavy', () => {
    expect(over(weight)).toEqual([expect.stringContaining('heaviest route')]);
  });

  it('prints the heaviest routes beside the totals', () => {
    expect(describeWeight(weight)).toContain('heaviest cold loads: /(app) 976.6 kB');
  });
});
