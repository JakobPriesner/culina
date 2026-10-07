import { describe, expect, it } from 'vitest';

import { closest, drifted, multiples, trim } from './quantityMath';

describe('drifted', () => {
  it('is true only past 2% of the exact amount', () => {
    expect(drifted(100, 102)).toBe(false);
    expect(drifted(100, 103)).toBe(true);
    expect(drifted(100, 97)).toBe(true);
  });

  it('is never true for an exact zero', () => {
    expect(drifted(0, 5)).toBe(false);
  });
});

describe('trim', () => {
  it('removes the floating-point tail', () => {
    expect(trim(0.1 + 0.2)).toBe(0.3);
  });
});

describe('multiples', () => {
  it('gives the steps either side of an amount', () => {
    expect(multiples(1.7, 0.5)).toEqual([1.5, 2]);
  });

  it('never goes below zero', () => {
    expect(multiples(0.2, 0.5)).toEqual([0, 0.5]);
  });
});

describe('closest', () => {
  it('picks the nearest candidate', () => {
    expect(closest(0.3, [0.125, 0.25, 0.5])).toBe(0.25);
  });
});
