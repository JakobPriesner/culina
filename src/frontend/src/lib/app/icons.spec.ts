import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

import { appIconFolder, appIcons } from './appIcons';

/*
 * The icon a browser prefers over every other one, and the only file in the app
 * that nothing else would ever have opened.
 *
 * It shipped unparseable: the comment at the top of it named the design tokens
 * its colours came from, and every token name begins with two hyphens, which is
 * the one sequence XML forbids inside a comment. Nothing warned. The build
 * copied it, the dev server served it with the right media type, and the file
 * was 2.3 KB of perfectly reasonable markup — the browser simply drew no icon,
 * for months, in the one place nobody screenshots.
 */
const staticDir = join(process.cwd(), 'static');
const read = (path: string) => readFileSync(join(staticDir, path), 'utf8');

/** What a browser would do with it: parse it as XML, or give up. */
function parseFailure(xml: string): string | null {
  const parsed = new DOMParser().parseFromString(xml, 'image/svg+xml');

  return parsed.querySelector('parsererror')?.textContent?.trim() ?? null;
}

describe.each(appIcons)('the %s SVG icon', (icon) => {
  const svg = () => read(`${appIconFolder(icon)}/icon.svg`);

  it('parses, which is the whole of whether it renders', () => {
    expect(parseFailure(svg())).toBeNull();
  });

  it('keeps its comments free of the sequence that broke it', () => {
    // Belt and braces: a parser reports the first failure, and this says which
    // one to look for. A hyphen pair is legal in an attribute — `stroke-width`
    // is not one — so only comment bodies are searched.
    const comments = svg().match(/<!--[\s\S]*?-->/g) ?? [];

    for (const comment of comments) {
      expect(comment.slice(4, -3)).not.toContain('--');
    }
  });

  it('is drawn in ink rather than in tokens, having no stylesheet to read', () => {
    // An icon is fetched as an image: no CSS custom property resolves in it,
    // and `var(--accent)` there is a shape with no colour at all.
    expect(svg()).not.toMatch(/var\(--/);
  });
});

/*
 * Each icon has a manifest of its own, written by `pnpm icons`, and a manifest
 * naming a file that is not there installs an app with no icon at all.
 */
describe.each(appIcons)('the %s manifest', (icon) => {
  const manifest = () =>
    JSON.parse(read(`${appIconFolder(icon)}/manifest.webmanifest`)) as {
      id: string;
      icons: { src: string; purpose: string }[];
      shortcuts: { icons: { src: string }[] }[];
      share_target: unknown;
    };

  it('names only files that exist', () => {
    const { icons, shortcuts } = manifest();
    const files = [...icons, ...shortcuts.flatMap((shortcut) => shortcut.icons)];

    for (const { src } of files) {
      expect(existsSync(join(staticDir, src)), src).toBe(true);
    }
  });

  it('names only its own files', () => {
    for (const { src } of manifest().icons) {
      expect(src.startsWith(`${appIconFolder(icon)}/`), src).toBe(true);
    }
  });

  it('is the same app, so an install keeps its identity whatever the icon', () => {
    expect(manifest().id).toBe('/');
  });

  it('receives the same links, captions and screenshots whatever the icon', () => {
    const target = JSON.parse(read('manifest.webmanifest')).share_target;
    expect(target).toMatchObject({
      action: '/recipes/import',
      method: 'POST',
      enctype: 'multipart/form-data',
      params: { title: 'title', text: 'text', url: 'url', files: expect.any(Array) }
    });
    expect(manifest().share_target).toEqual(target);
  });

  it('has a shape for every mask a launcher cuts and for themed icons', () => {
    const purposes = manifest().icons.map((entry) => entry.purpose);

    expect(purposes).toEqual(expect.arrayContaining(['any', 'maskable', 'monochrome']));
  });
});
