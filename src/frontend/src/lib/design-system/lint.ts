import { glob, readFile } from 'node:fs/promises';

/**
 * The design system's two build-time rules.
 *
 * The point of three layers is that re-theming the app means editing one
 * directory. A single `#fff` in a component quietly breaks that promise and is
 * invisible in review, so `themes.spec.ts` turns it into a failing test.
 *
 * A colour written outside the token system quietly breaks the promise that
 * re-theming means editing one directory, and a `var(--space-5)` that nobody
 * declared renders as nothing at all — a 200-pixel icon where a 20-pixel one
 * was meant. Both are invisible in review, so `lint.spec.ts` turns them into
 * failing tests.
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

const lineOf = (css: string, index: number) => css.slice(0, index).split('\n').length;

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
        violations.push(`${path}:${lineOf(css, match.index)} uses ${name} (${match[0].trim()})`);
      }
    }
  }

  return violations.sort();
}

/** Explains the rule once, under the list of what broke it. */
export const rawColourRemedy =
  'Colours belong to the token system: add or reuse a semantic token in ' +
  'src/lib/design-system/tokens/semantic.css and give every theme a value for it.';

/** A custom property being given a value in CSS, rather than being read. */
const declaration = /(^|[;{])\s*(--[\w-]+)\s*:/g;

/**
 * Svelte's `style:--name={...}` directive, which sets a custom property on the
 * element without ever appearing in a stylesheet. It is a declaration; a
 * scanner that only reads CSS would call every use of it undeclared.
 */
const styleDirective = /\bstyle:(--[\w-]+)/g;

/** Where the vocabulary of tokens is defined. */
const tokenDirectories = ['src/lib/design-system/tokens/', 'src/lib/design-system/themes/'];

/**
 * Finds `var(--token)` references to a token nothing declares.
 *
 * A custom property that does not exist is not an error in CSS: the declaration
 * is simply dropped, and the element falls back to whatever the initial value
 * is. The scale is deliberately sparse — there is no `--space-5` — so reaching
 * for a step that sounds plausible is an easy mistake with a loud result and no
 * warning.
 */
export async function findUnknownTokens(root: string): Promise<string[]> {
  const declared = new Set<string>();
  const files: { path: string; css: string }[] = [];
  const markup = new Map<string, string[]>();

  for await (const entry of glob('src/**/*.{css,svelte}', { cwd: root })) {
    const path = entry.replaceAll('\\', '/');
    const contents = await readFile(`${root}/${path}`, 'utf8');
    const css = withoutComments(styleSource(path, contents));

    files.push({ path, css });
    markup.set(
      path,
      [...contents.matchAll(styleDirective)].map(([, name]) => name!)
    );

    if (tokenDirectories.some((directory) => path.startsWith(directory))) {
      for (const [, , name] of css.matchAll(declaration)) {
        declared.add(name!);
      }
    }
  }

  const violations: string[] = [];

  for (const { path, css } of files) {
    // A component may declare a property of its own and use it in the same
    // file; that is local plumbing, not a missing token.
    const local = new Set([
      ...[...css.matchAll(declaration)].map(([, , name]) => name!),
      ...(markup.get(path) ?? [])
    ]);

    for (const match of css.matchAll(/var\(\s*(--[\w-]+)/g)) {
      const name = match[1]!;

      if (declared.has(name) || local.has(name)) {
        continue;
      }

      violations.push(`${path}:${lineOf(css, match.index)} uses ${name}, which nothing declares`);
    }
  }

  return violations.sort();
}

export const unknownTokenRemedy =
  'Use a step that exists, or add one to src/lib/design-system/tokens/scales.css. ' +
  'The scale is sparse on purpose: fewer choices make spacing consistent.';
