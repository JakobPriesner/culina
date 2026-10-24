import { describe, expect, it } from 'vitest';

import { readGtin } from './gtin';

describe('reading a barcode', () => {
  it.each([
    ['8076800195057', '8076800195057'],
    [' 8076 800195 057 ', '8076800195057'],
    ['96385074', '96385074'],
    ['10012345678902', '10012345678902']
  ])('keeps an EAN-8, EAN-13 or GTIN-14 as it is: %s', (typed, expected) => {
    expect(readGtin(typed)).toBe(expected);
  });

  it('pads a UPC-A to 13 digits, so it meets the EAN-13 of the same product', () => {
    expect(readGtin('042100005264')).toBe('0042100005264');
  });

  it.each([
    ['04252614', '0042100005264'],
    ['00123430', '0001200000340'],
    ['00123444', '0001230000044']
  ])('expands a UPC-E to its UPC-A: %s', (upcE, expected) => {
    expect(readGtin(upcE)).toBe(expected);
  });

  it('reads an eight-digit code that fits both symbologies as an EAN-8, as the server does', () => {
    expect(readGtin('01234565')).toBe('01234565');
  });

  it.each([
    '',
    '   ',
    '8076800195058',
    '80768001950AB',
    '123456789',
    '123456789012345',
    '96385075'
  ])('refuses what is no barcode: "%s"', (typed) => {
    expect(readGtin(typed)).toBeNull();
  });
});
