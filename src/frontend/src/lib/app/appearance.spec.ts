import { describe, expect, it } from 'vitest';

import { defaultAppearance, nextMode, parseAppearance } from './appearance';

/*
 * The stored value is editable by hand and outlives releases, so every branch
 * here is about surviving a value we did not write.
 */
describe('reading a stored appearance', () => {
  it('accepts what we wrote', () => {
    expect(parseAppearance('{"theme":"warm-paper","mode":"dark"}')).toEqual({
      theme: 'warm-paper',
      mode: 'dark'
    });
  });

  it.each([
    ['nothing stored', null],
    ['an empty string', ''],
    ['not JSON at all', 'warm-paper'],
    ['a JSON primitive', '"dark"'],
    ['a theme that no longer exists', '{"theme":"midnight","mode":"dark"}'],
    ['a mode that never existed', '{"theme":"warm-paper","mode":"sepia"}']
  ])('falls back rather than rendering an unthemed page: %s', (_, stored) => {
    const appearance = parseAppearance(stored);

    expect(appearance.theme).toBe(defaultAppearance.theme);
  });

  it('keeps the half it can use', () => {
    expect(parseAppearance('{"theme":"warm-paper","mode":"sepia"}')).toEqual({
      theme: 'warm-paper',
      mode: defaultAppearance.mode
    });
  });
});

describe('the appearance toggle', () => {
  it('cycles light, dark, then back to following the device', () => {
    expect(nextMode('light')).toBe('dark');
    expect(nextMode('dark')).toBe('system');
    expect(nextMode('system')).toBe('light');
  });
});
