import { readdir, readFile } from 'node:fs/promises';
import { describe, expect, it } from 'vitest';

import { contrastRatio, parseColour, type Rgb } from './colour';
import { declaredProperties, readBlocks, type CssBlock } from './css';
import { findRawColours, rawColourRemedy } from './rawColours';
import { themes } from './index';

/*
 * The promise of a three-layer token system is that adding a theme is one new
 * file. This suite is what makes that true: it derives the contract from
 * `semantic.css` rather than restating it, so a token added tomorrow is
 * required of every theme without anyone remembering to update a test.
 */

const root = 'src/lib/design-system';
const themeDirectory = `${root}/themes`;

/** WCAG AA: body text, and anything that identifies a control or its state. */
const forText = 4.5;
const forNonText = 3;

/**
 * The pairs a theme is actually allowed to get wrong.
 *
 * Only combinations the UI really stacks are listed. A table claiming every
 * possible pair would be false confidence and would block reasonable palettes.
 */
const requiredContrast = [
  { foreground: '--text', background: '--surface', minimum: forText },
  { foreground: '--text', background: '--surface-raised', minimum: forText },
  { foreground: '--text', background: '--surface-sunken', minimum: forText },
  { foreground: '--text', background: '--surface-overlay', minimum: forText },
  { foreground: '--text', background: '--surface-hover', minimum: forText },
  { foreground: '--text', background: '--surface-selected', minimum: forText },
  { foreground: '--text', background: '--surface-accent-subtle', minimum: forText },
  { foreground: '--text', background: '--danger-subtle', minimum: forText },
  { foreground: '--text', background: '--success-subtle', minimum: forText },
  { foreground: '--text', background: '--warning-subtle', minimum: forText },
  { foreground: '--text-muted', background: '--surface', minimum: forText },
  { foreground: '--text-muted', background: '--surface-raised', minimum: forText },
  { foreground: '--text-subtle', background: '--surface', minimum: forText },
  { foreground: '--text-danger', background: '--surface', minimum: forText },
  { foreground: '--text-success', background: '--surface', minimum: forText },
  { foreground: '--text-on-accent', background: '--accent', minimum: forText },
  { foreground: '--text-on-accent', background: '--accent-hover', minimum: forText },
  { foreground: '--accent-contrast', background: '--accent', minimum: forText },
  // Non-text: the border that identifies an input, the focus ring, and the
  // status colours — which always accompany a word or an icon rather than
  // carrying the meaning alone.
  { foreground: '--accent', background: '--surface', minimum: forNonText },
  { foreground: '--border-strong', background: '--surface', minimum: forNonText },
  { foreground: '--border-focus', background: '--surface', minimum: forNonText },
  { foreground: '--danger', background: '--surface', minimum: forNonText },
  { foreground: '--success', background: '--surface', minimum: forNonText },
  { foreground: '--warning', background: '--surface', minimum: forNonText }
];

const modes = [
  { name: 'light', selector: (id: string) => `[data-theme='${id}']` },
  { name: 'dark', selector: (id: string) => `[data-theme='${id}'][data-mode='dark']` }
];

const read = (path: string) => readFile(path, 'utf8');

/** Every token `semantic.css` declares — the contract each theme must fulfil. */
const contract = [...declaredProperties(await read(`${root}/tokens/semantic.css`))].sort();

/** Layer 1, by name, so a theme's `var(--c-…)` can be turned into a colour. */
const primitives = new Map(
  readBlocks(await read(`${root}/tokens/primitives.css`)).flatMap((block) => [
    ...block.declarations
  ])
);

function resolve(value: string | undefined): Rgb | null {
  if (value === undefined) {
    return null;
  }

  const reference = value.match(/^var\((--[\w-]+)\)$/);

  return parseColour(reference ? (primitives.get(reference[1]!) ?? '') : value);
}

const themeIds = (await readdir(themeDirectory))
  .filter((name) => name.endsWith('.css'))
  .map((name) => name.replace(/\.css$/, ''))
  .sort();

const themeBlocks = new Map<string, CssBlock[]>(
  await Promise.all(
    themeIds.map(async (id) => [id, readBlocks(await read(`${themeDirectory}/${id}.css`))] as const)
  )
);

const blockFor = (themeId: string, selector: string) =>
  themeBlocks.get(themeId)?.find((block) => block.selector === selector)?.declarations;

describe('the semantic token contract', () => {
  it('is not accidentally empty', () => {
    expect(contract.length).toBeGreaterThan(30);
  });

  it('is fulfilled by at least one theme', () => {
    expect(themeIds).not.toEqual([]);
  });
});

describe.each(themeIds)('the %s theme', (themeId) => {
  describe.each(modes)('in $name mode', ({ selector }) => {
    const declarations = blockFor(themeId, selector(themeId));

    it('has a block of its own', () => {
      expect(declarations, `expected a rule for ${selector(themeId)}`).toBeDefined();
    });

    it('defines every token in the contract', () => {
      const missing = contract.filter((token) => !declarations?.has(token));

      expect(
        missing,
        'a token a theme forgets falls back to the light-mode default, which in ' +
          'dark mode can be black text on a black ground'
      ).toEqual([]);
    });

    it('defines nothing outside the contract', () => {
      const extra = [...(declarations?.keys() ?? [])].filter((token) => !contract.includes(token));

      expect(extra, 'no component can read a token that the contract does not declare').toEqual([]);
    });

    it('resolves every reference to a real primitive', () => {
      const unresolved = [...(declarations?.entries() ?? [])]
        .filter(([, value]) => value.startsWith('var(') && !resolve(value))
        .map(([token, value]) => `${token}: ${value}`);

      expect(unresolved).toEqual([]);
    });

    it.each(requiredContrast)(
      '$foreground on $background reaches $minimum:1',
      ({ foreground, background, minimum }) => {
        const first = resolve(declarations?.get(foreground));
        const second = resolve(declarations?.get(background));

        expect(first, `${foreground} is not a resolvable colour`).not.toBeNull();
        expect(second, `${background} is not a resolvable colour`).not.toBeNull();

        const ratio = contrastRatio(first!, second!);

        expect(
          Number(ratio.toFixed(2)),
          `${foreground} on ${background} is only ${ratio.toFixed(2)}:1`
        ).toBeGreaterThanOrEqual(minimum);
      }
    );
  });
});

describe('the theme registry', () => {
  it('lists exactly the themes that have a stylesheet', () => {
    expect(themes.map((theme) => theme.id).sort()).toEqual(themeIds);
  });

  it('gives every theme a label a person could read', () => {
    for (const theme of themes) {
      expect(theme.label.trim()).not.toBe('');
    }
  });
});

describe('the raw-colour rule', () => {
  it('finds no colour written outside the token system', async () => {
    expect(await findRawColours('.'), rawColourRemedy).toEqual([]);
  });
});
