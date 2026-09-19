import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

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
const read = (name: string) => readFileSync(join(process.cwd(), 'static', name), 'utf8');

/** What a browser would do with it: parse it as XML, or give up. */
function parseFailure(xml: string): string | null {
  const parsed = new DOMParser().parseFromString(xml, 'image/svg+xml');

  return parsed.querySelector('parsererror')?.textContent?.trim() ?? null;
}

describe('the SVG icon', () => {
  it('parses, which is the whole of whether it renders', () => {
    expect(parseFailure(read('icon.svg'))).toBeNull();
  });

  it('keeps its comments free of the sequence that broke it', () => {
    // Belt and braces: a parser reports the first failure, and this says which
    // one to look for. A hyphen pair is legal in an attribute — `stroke-width`
    // is not one — so only comment bodies are searched.
    const comments = read('icon.svg').match(/<!--[\s\S]*?-->/g) ?? [];

    for (const comment of comments) {
      expect(comment.slice(4, -3)).not.toContain('--');
    }
  });

  it('is drawn in ink rather than in tokens, having no stylesheet to read', () => {
    // An icon is fetched as an image: no CSS custom property resolves in it,
    // and `var(--accent)` there is a shape with no colour at all.
    expect(read('icon.svg')).not.toMatch(/var\(--/);
  });
});
