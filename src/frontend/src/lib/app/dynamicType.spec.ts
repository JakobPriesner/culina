import { describe, expect, it } from 'vitest';

import { textScale } from './dynamicType';

describe('textScale', () => {
  it('leaves the default text size alone', () => {
    expect(textScale(17)).toBe(1);
  });

  it('follows a larger setting in proportion', () => {
    expect(textScale(23)).toBeCloseTo(23 / 17);
  });

  it('stays within the sizes iOS offers', () => {
    expect(textScale(4)).toBeCloseTo(14 / 17);
    expect(textScale(200)).toBeCloseTo(53 / 17);
  });

  it('ignores a reading that is not a size', () => {
    expect(textScale(Number.NaN)).toBe(1);
    expect(textScale(0)).toBe(1);
  });
});
