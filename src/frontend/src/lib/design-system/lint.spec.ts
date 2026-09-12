import { describe, expect, it } from 'vitest';

import { findRawColours, findUnknownTokens, rawColourRemedy, unknownTokenRemedy } from './lint';

/*
 * Two rules, both about things CSS fails at silently: an unthemed colour still
 * renders, and an undeclared custom property simply drops the declaration. Only
 * a build can notice either.
 */
describe('the design-system rules', () => {
  it('find no colour written outside the token system', async () => {
    expect(await findRawColours('.'), rawColourRemedy).toEqual([]);
  });

  it('find no reference to a token nothing declares', async () => {
    expect(await findUnknownTokens('.'), unknownTokenRemedy).toEqual([]);
  });
});
