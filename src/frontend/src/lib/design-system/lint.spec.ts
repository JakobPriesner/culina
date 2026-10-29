import { describe, expect, it } from 'vitest';

import {
  findRawColours,
  findStrayGlass,
  findUnknownTokens,
  rawColourRemedy,
  strayGlassRemedy,
  unknownTokenRemedy
} from './lint';

/*
 * Rules about silent CSS failures: an unthemed colour still renders, an undeclared custom property drops the
 * declaration, and a blur written in place ignores Reduce Transparency.
 */
describe('the design-system rules', () => {
  it('find no colour written outside the token system', async () => {
    expect(await findRawColours('.'), rawColourRemedy).toEqual([]);
  });

  it('find no reference to a token nothing declares', async () => {
    expect(await findUnknownTokens('.'), unknownTokenRemedy).toEqual([]);
  });

  it('find no glass but the one token', async () => {
    expect(await findStrayGlass('.'), strayGlassRemedy).toEqual([]);
  });
});
