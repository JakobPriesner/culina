import { readdir, readFile } from 'node:fs/promises';
import { describe, expect, it } from 'vitest';

import { contrastRatio, parseColour, type Rgb } from './themes/colour';
import { declaredProperties, readBlocks, type CssBlock } from './themes/css';
import { themes } from './themes';

/* A theme is one new file: this derives the contract from `semantic.css` instead of restating it, so a new token is required of every theme automatically. */

const root = 'src/lib/design-system';
const themeDirectory = `${root}/themes`;

/** WCAG AA: body text, and anything that identifies a control or its state. */
const forText = 4.5;
const forNonText = 3;

/** The pairs a theme may get wrong: only combinations the UI really stacks, since a table of every pair would block reasonable palettes. */
const requiredContrast = [
  { foreground: '--text-on-feature', background: '--surface-feature', minimum: forText },
  { foreground: '--text', background: '--surface', minimum: forText },
  { foreground: '--text', background: '--surface-raised', minimum: forText },
  { foreground: '--text', background: '--surface-sunken', minimum: forText },
  { foreground: '--text', background: '--surface-overlay', minimum: forText },
  { foreground: '--text', background: '--surface-hover', minimum: forText },
  { foreground: '--text', background: '--surface-selected', minimum: forText },
  { foreground: '--text', background: '--surface-accent-subtle', minimum: forText },
  { foreground: '--text', background: '--surface-highlight', minimum: forText },
  { foreground: '--text-muted', background: '--surface-highlight', minimum: forText },
  { foreground: '--text', background: '--danger-subtle', minimum: forText },
  { foreground: '--text', background: '--success-subtle', minimum: forText },
  { foreground: '--text', background: '--warning-subtle', minimum: forText },
  { foreground: '--text-muted', background: '--surface', minimum: forText },
  { foreground: '--text-muted', background: '--surface-raised', minimum: forText },
  { foreground: '--text', background: '--field-surface', minimum: forText },
  { foreground: '--text-subtle', background: '--field-surface', minimum: forText },
  { foreground: '--text', background: '--field-surface-inset', minimum: forText },
  { foreground: '--text-subtle', background: '--field-surface-inset', minimum: forText },
  // A disabled field is transparent: its text sits on the page or on the group around it.
  { foreground: '--field-text-disabled', background: '--surface', minimum: forText },
  { foreground: '--field-text-disabled', background: '--surface-raised', minimum: forText },
  { foreground: '--text-subtle', background: '--surface', minimum: forText },
  { foreground: '--text-subtle', background: '--surface-sunken', minimum: forText },
  { foreground: '--text-danger', background: '--surface', minimum: forText },
  { foreground: '--text-success', background: '--surface', minimum: forText },
  { foreground: '--text-on-accent', background: '--accent', minimum: forText },
  { foreground: '--text-on-accent', background: '--accent-hover', minimum: forText },
  { foreground: '--accent-contrast', background: '--accent', minimum: forText },
  // The phone dock's current destination: its icon and label in the accent on the raised lens.
  { foreground: '--accent', background: '--surface-raised', minimum: forText },
  // An ingredient named in a step: tinted words on the page, on its pointed-at tint and on its pressed one.
  { foreground: '--accent', background: '--surface', minimum: forText },
  { foreground: '--accent', background: '--surface-accent-subtle', minimum: forText },
  { foreground: '--accent', background: '--surface-highlight', minimum: forText },
  // Non-text: input border, focus ring and status colours, which always accompany a word or icon.
  { foreground: '--accent', background: '--surface', minimum: forNonText },
  // The dot after the name: decoration, but an invisible one is a typo.
  { foreground: '--brand-accent', background: '--surface', minimum: forNonText },
  { foreground: '--border-strong', background: '--surface', minimum: forNonText },
  { foreground: '--border-focus', background: '--surface', minimum: forNonText },
  { foreground: '--field-border', background: '--field-surface', minimum: forNonText },
  { foreground: '--field-border', background: '--field-surface-inset', minimum: forNonText },
  { foreground: '--field-border-focus', background: '--field-surface', minimum: forNonText },
  { foreground: '--field-border-error', background: '--field-surface', minimum: forNonText },
  { foreground: '--danger', background: '--surface', minimum: forNonText },
  { foreground: '--success', background: '--surface', minimum: forNonText },
  { foreground: '--warning', background: '--surface', minimum: forNonText }
];

/** Increase Contrast asks more of the quieter pairs: muted text at AAA and edges a person can find. */
const moreContrast = [
  { foreground: '--text-muted', background: '--surface', minimum: 7 },
  { foreground: '--text-muted', background: '--surface-raised', minimum: 7 },
  { foreground: '--border-strong', background: '--surface', minimum: forText },
  { foreground: '--field-border', background: '--field-surface', minimum: forText },
  { foreground: '--border-focus', background: '--surface', minimum: forText }
];

/**
 * A contrast block holds only what Increase Contrast changes, so it is read over its mode's block: every check runs
 * on what the page would really use.
 */
const modes = [
  { name: 'light', selector: (id: string) => `[data-theme='${id}']`, pairs: requiredContrast },
  {
    name: 'dark',
    selector: (id: string) => `[data-theme='${id}'][data-mode='dark']`,
    pairs: requiredContrast
  },
  {
    name: 'light with more contrast',
    selector: (id: string) => `[data-theme='${id}'][data-mode='light'][data-contrast='more']`,
    over: (id: string) => `[data-theme='${id}']`,
    pairs: [...requiredContrast, ...moreContrast]
  },
  {
    name: 'dark with more contrast',
    selector: (id: string) => `[data-theme='${id}'][data-mode='dark'][data-contrast='more']`,
    over: (id: string) => `[data-theme='${id}'][data-mode='dark']`,
    pairs: [...requiredContrast, ...moreContrast]
  }
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
  describe.each(modes)('in $name mode', ({ selector, over, pairs }) => {
    const own = blockFor(themeId, selector(themeId));
    const declarations =
      own && new Map([...(over ? (blockFor(themeId, over(themeId)) ?? []) : []), ...own]);

    it('has a block of its own', () => {
      expect(own, `expected a rule for ${selector(themeId)}`).toBeDefined();
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
      const extra = [...(own?.keys() ?? [])].filter((token) => !contract.includes(token));

      expect(extra, 'no component can read a token that the contract does not declare').toEqual([]);
    });

    it('resolves every reference to a real primitive', () => {
      const unresolved = [...(declarations?.entries() ?? [])]
        .filter(([, value]) => value.startsWith('var(') && !resolve(value))
        .map(([token, value]) => `${token}: ${value}`);

      expect(unresolved).toEqual([]);
    });

    it.each(pairs)(
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
