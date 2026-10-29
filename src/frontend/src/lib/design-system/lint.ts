import { glob, readFile } from 'node:fs/promises';

/**
 * The design system's two build-time rules: no raw colours outside the token layers, and no
 * `var(--x)` nobody declared (it renders as nothing). Only CSS is scanned: TypeScript would flag "black" in a comment.
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

export const rawColourRemedy =
  'Colours belong to the token system: add or reuse a semantic token in ' +
  'src/lib/design-system/tokens/semantic.css and give every theme a value for it.';

const declaration = /(^|[;{])\s*(--[\w-]+)\s*:/g;

/** Svelte's `style:--name` sets a custom property without appearing in a stylesheet, so it counts as a declaration. */
const styleDirective = /\bstyle:(--[\w-]+)/g;

const tokenDirectories = ['src/lib/design-system/tokens/', 'src/lib/design-system/themes/'];

/** Finds `var(--token)` references to an undeclared token: CSS silently drops them, and the scale is sparse (no `--space-5`). */
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
    // A property a component declares and uses itself is local plumbing.
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

/** Where a backdrop blur may be spelled out: the token itself, and the scroll edge effect's stacked ramp. */
const glassOwners = [
  'src/lib/design-system/tokens/',
  'src/lib/design-system/display/EdgeEffect.svelte'
];

/**
 * Finds a backdrop-filter that is not the one glass (`var(--glass-filter)`) or `none`, and any `-webkit-` twin of it.
 * A blur written in place is a glass Reduce Transparency cannot turn off, and the ninth slightly different one nobody
 * meant. The twin is worse than redundant: the build's minifier folds a `var()` pair into the prefixed property alone,
 * which Chrome and Firefox ignore, so the glass silently vanishes there; the minifier adds the prefix itself.
 */
export async function findStrayGlass(root: string): Promise<string[]> {
  const violations: string[] = [];

  for await (const entry of glob('src/**/*.{css,svelte}', { cwd: root })) {
    const path = entry.replaceAll('\\', '/');

    if (glassOwners.some((owner) => path.startsWith(owner))) {
      continue;
    }

    const css = withoutComments(styleSource(path, await readFile(`${root}/${path}`, 'utf8')));

    for (const match of css.matchAll(/(-webkit-)?backdrop-filter\s*:\s*([^;}]+)/g)) {
      const value = match[2]!.trim();

      if (match[1]) {
        violations.push(`${path}:${lineOf(css, match.index)} writes the -webkit- twin (${value})`);
      } else if (value !== 'var(--glass-filter)' && value !== 'none') {
        violations.push(`${path}:${lineOf(css, match.index)} blurs with ${value}`);
      }
    }
  }

  return violations.sort();
}

export const strayGlassRemedy =
  'Glass is one token: write `backdrop-filter: var(--glass-filter)` with no -webkit- twin, and only on what ' +
  'floats above content. A different strength belongs in tokens/scales.css, not in a component.';
