import { glob, readFile } from 'node:fs/promises';

/**
 * Finds colours written outside the token system.
 *
 * The point of three layers is that re-theming the app means editing one
 * directory. A single `#fff` in a component quietly breaks that promise and is
 * invisible in review, so `themes.spec.ts` turns it into a failing test.
 *
 * Only CSS is examined: stylesheets, `<style>` blocks and inline `style`
 * attributes. Scanning TypeScript would flag the word "black" in a comment and
 * teach everyone to ignore the rule.
 */

/** Colours and layer-1 primitives may only be written here. */
const allowedDirectories = ['src/lib/design-system/tokens/', 'src/lib/design-system/themes/'];

const rules = [
  { name: 'a hex colour', pattern: /#[0-9a-f]{3,8}\b/gi },
  {
    name: 'a colour function',
    pattern: /\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch|color-mix)\s*\(/gi
  },
  {
    name: 'a named colour',
    pattern: /:\s*(?:white|black|red|green|blue|grey|gray|silver)\s*[;!)]/gi
  },
  { name: 'a layer-1 primitive', pattern: /--c-[a-z]+-\d+/gi }
] as const;

/** The CSS in a file: the whole thing, or the parts of a component that style it. */
function styleSource(path: string, contents: string): string {
  if (path.endsWith('.css')) {
    return contents;
  }

  const styles = [...contents.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/gi)].map(
    (match) => match[1] ?? ''
  );
  const inline = [...contents.matchAll(/\bstyle=(?:"([^"]*)"|'([^']*)')/gi)].map(
    (match) => match[1] ?? match[2] ?? ''
  );

  return [...styles, ...inline].join('\n');
}

const withoutComments = (css: string) => css.replace(/\/\*[\s\S]*?\*\//g, '');

const isAllowed = (path: string) =>
  allowedDirectories.some((directory) => path.startsWith(directory));

/** Every raw colour written outside the token system, as readable messages. */
export async function findRawColours(root: string): Promise<string[]> {
  const violations: string[] = [];

  for await (const entry of glob('src/**/*.{css,svelte}', { cwd: root })) {
    const path = entry.replaceAll('\\', '/');

    if (isAllowed(path)) {
      continue;
    }

    const css = withoutComments(styleSource(path, await readFile(`${root}/${path}`, 'utf8')));

    for (const { name, pattern } of rules) {
      for (const match of css.matchAll(pattern)) {
        const line = css.slice(0, match.index).split('\n').length;

        violations.push(`${path}:${line} uses ${name} (${match[0].trim()})`);
      }
    }
  }

  return violations.sort();
}

/** Explains the rule once, under the list of what broke it. */
export const rawColourRemedy =
  'Colours belong to the token system: add or reuse a semantic token in ' +
  'src/lib/design-system/tokens/semantic.css and give every theme a value for it.';
