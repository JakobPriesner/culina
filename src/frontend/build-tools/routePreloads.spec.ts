import { describe, expect, it } from 'vitest';

import { parseDictionary, patternOf, plan, preloadedBy, script } from './routePreloads';
import type { Manifest } from './routePreloads';

/* The table decides what a cold visit fetches up front: a wrong row either preloads nothing useful or fetches what the page never uses. */
const file = (name: string) => `_app/immutable/${name}`;
const node = (id: number) => `.svelte-kit/generated/client-optimized/nodes/${id}.js`;

const manifest: Manifest = {
  [node(0)]: { file: file('nodes/0.a.js'), imports: ['_shell.js', '_layout.js'] },
  [node(2)]: { file: file('nodes/2.b.js'), imports: ['_shell.js', '_layout.js', '_app.js'] },
  [node(4)]: { file: file('nodes/4.c.js'), imports: ['_shell.js'] },
  [node(6)]: { file: file('nodes/6.d.js'), imports: ['_app.js', '_deep.js'] },
  [node(16)]: { file: file('nodes/16.e.js'), imports: ['_app.js'] },
  [node(25)]: { file: file('nodes/25.f.js'), imports: ['_form.js'] },
  '_shell.js': { file: file('chunks/shell.js') },
  '_layout.js': { file: file('chunks/layout.js') },
  '_app.js': { file: file('chunks/app.js'), imports: ['_deep.js'] },
  '_deep.js': { file: file('chunks/deep.js') },
  '_form.js': { file: file('chunks/form.js') }
};

const dictionary = parseDictionary(`export const dictionary = {
		"/(app)": [6,[2]],
		"/(auth)/login": [25,[4]],
		"/(app)/recipes/[recipeId]": [16,[2]],
		"/design": [30]
	};`);

const shell = new Set([file('chunks/shell.js'), file('nodes/0.a.js')]);

describe('the route table', () => {
  const table = plan(manifest, { ...dictionary, '/design': [6] }, shell);
  const names = (id: number) => table.nodes[id]!.map((at) => table.files[at]);

  it('reads the route ids out of the generated route table', () => {
    expect(dictionary['/(app)/recipes/[recipeId]']).toEqual([16, [2]]);
    expect(dictionary['/design']).toEqual([30, []]);
  });

  it('writes parameters as a star and drops the groups', () => {
    expect(patternOf('/(app)')).toBe('/');
    expect(patternOf('/(app)/recipes/[recipeId]/edit')).toBe('/recipes/*/edit');
    expect(patternOf('/(auth)/join/[code]')).toBe('/join/*');
    expect(() => patternOf('/(app)/files/[...path]')).toThrow(/rest parameters/);
  });

  it('leaves out what the shell already preloads', () => {
    expect(names(0)).toEqual(['chunks/layout.js']);
  });

  it('gives a layout only what is not the root layouts, and a page only what its layouts do not', () => {
    expect(names(2)).toEqual(['nodes/2.b.js', 'chunks/app.js', 'chunks/deep.js']);
    expect(names(6)).toEqual(['nodes/6.d.js']);
    expect(names(16)).toEqual(['nodes/16.e.js']);
    expect(names(25)).toEqual(['nodes/25.f.js', 'chunks/form.js']);
  });

  it('asks for the session behind sign-in and the setup stage on the sign-in pages only', () => {
    const early = Object.fromEntries(table.routes.map((route) => [route.pattern, route.early]));

    expect(early).toMatchObject({ '/': 0, '/login': 1, '/recipes/*': 0, '/design': null });
  });

  it('finds the shell’s own preloads', () => {
    const html =
      '<link href="/_app/immutable/chunks/a.js" rel="modulepreload">\n<link href="/x.css" rel="stylesheet">';

    expect([...preloadedBy(html)]).toEqual(['_app/immutable/chunks/a.js']);
  });
});

describe('the shell script', () => {
  const source = script(plan(manifest, dictionary, shell));

  function visit(path: string, controlled = false) {
    const links: Record<string, unknown>[] = [];
    const head = { appendChild: (link: Record<string, unknown>) => links.push(link) };
    const document = { head, createElement: () => ({}) };
    const navigator = { serviceWorker: controlled ? { controller: {} } : { controller: null } };

    new Function('document', 'location', 'navigator', source)(
      document,
      { pathname: path },
      navigator
    );

    return links.map(
      (link) => `${link['rel']} ${link['href']}${link['as'] ? ` as=${link['as']}` : ''}`
    );
  }

  it('preloads the layouts and page of the route being opened, and the first requests', () => {
    expect(visit('/')).toEqual([
      'modulepreload /_app/immutable/chunks/layout.js',
      'modulepreload /_app/immutable/nodes/2.b.js',
      'modulepreload /_app/immutable/chunks/app.js',
      'modulepreload /_app/immutable/chunks/deep.js',
      'modulepreload /_app/immutable/nodes/6.d.js',
      'preload /api/v1/users/me as=fetch',
      'preload /api/v1/users/me/settings as=fetch'
    ]);
  });

  it('matches a parameter segment', () => {
    expect(visit('/recipes/01abc')).toContain('modulepreload /_app/immutable/nodes/16.e.js');
    expect(visit('/recipes/01abc/extra')).toEqual([]);
  });

  it('asks for the setup stage on a sign-in page', () => {
    expect(visit('/login')).toContain('preload /api/v1/setup as=fetch');
  });

  it('keeps the data requests to itself when a service worker will answer', () => {
    const links = visit('/', true);

    expect(links.some((link) => link.startsWith('modulepreload'))).toBe(true);
    expect(links.some((link) => link.startsWith('preload'))).toBe(false);
  });

  it('adds nothing for an address with no row', () => {
    expect(visit('/nowhere')).toEqual([]);
  });
});
