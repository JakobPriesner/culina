import { readFile } from 'node:fs/promises';
import { expect, it } from 'vitest';
import { contrastRatio, parseColour } from './themes/colour';
import { declaredProperties, readBlocks } from './themes/css';

const read = (name: string) => readFile(`src/lib/design-system/${name}`, 'utf8');
const contract = [...declaredProperties(await read('tokens/semantic.css'))];
const primitives = new Map(
  readBlocks(await read('tokens/primitives.css')).flatMap((block) => [...block.declarations])
);
const source = await read('tokens/kitchen-lighting.css');
const blocks = readBlocks(source);
const resolve = (value: string) => {
  const ref = value.match(/^var\((--[\w-]+)\)$/);
  return parseColour(ref ? primitives.get(ref[1]!)! : value)!;
};

it.each(['glare', 'oled'])('%s defines the full semantic contract', (mode) => {
  const block = blocks.find((block) => block.selector.includes(`data-kitchen-lighting='${mode}'`))!;
  expect(block).toBeDefined();
  expect(contract.filter((token) => !block.declarations.has(token))).toEqual([]);
  const minimum = mode === 'glare' ? 7 : 4.5;
  for (const surface of [
    '--surface',
    '--surface-raised',
    '--surface-sunken',
    '--surface-overlay',
    '--surface-hover',
    '--surface-selected',
    '--surface-highlight'
  ]) {
    for (const text of ['--text', '--text-muted', '--text-subtle']) {
      const ratio = contrastRatio(
        resolve(block.declarations.get(text)!),
        resolve(block.declarations.get(surface)!)
      );
      expect(ratio, `${text} on ${surface}`).toBeGreaterThanOrEqual(minimum);
    }
  }
  for (const accent of ['--accent', '--accent-hover']) {
    expect(
      contrastRatio(
        resolve(block.declarations.get('--text-on-accent')!),
        resolve(block.declarations.get(accent)!)
      )
    ).toBeGreaterThanOrEqual(4.5);
  }
  if (mode === 'oled') {
    for (const token of ['--surface', '--surface-raised', '--surface-overlay'])
      expect(block.declarations.get(token)).toBe('#000000');
    expect(block.declarations.get('--shadow-glass')).toBe('none');
  }
});
it('lets printing and forced colours keep their own appearance', () => {
  expect(source).toContain('@media screen and (forced-colors: none)');
});
