import { beforeEach, describe, expect, it } from 'vitest';

import { gramsField, gramsWords, parseGrams } from './weights';
import { preferences } from '$shell/preferences.svelte';

beforeEach(() => preferences.setLocale('en'));

describe('a weight typed in grams', () => {
  it('takes a comma or a point, and what the server takes', () => {
    expect(parseGrams('150')).toBe(150);
    expect(parseGrams(' 4,5 ')).toBe(4.5);
    expect(parseGrams('4.5')).toBe(4.5);
    expect(parseGrams('10000')).toBe(10000);
    expect(parseGrams('0.1')).toBe(0.1);
  });

  it('refuses nothing, nought, too much and words', () => {
    for (const typed of ['', '  ', '0', '-1', '10000.5', '10001', 'abc', '1e9x']) {
      expect(parseGrams(typed)).toBeNull();
    }
  });

  it('is written back the way it was typed, without a thousands separator', () => {
    expect(gramsField(1200)).toBe('1200');
    expect(gramsField(4.5)).toBe('4.5');

    preferences.setLocale('de');

    expect(gramsField(4.5)).toBe('4,5');
    expect(gramsWords(1200)).toBe('1.200');
  });
});
