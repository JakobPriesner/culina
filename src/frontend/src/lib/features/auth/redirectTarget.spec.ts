import { describe, expect, it } from 'vitest';

import { safeRedirect } from './redirectTarget';

/*
 * This value arrives in a URL that anyone can write, so every case here is a
 * link somebody could send to a person who trusts Culina.
 */
describe('where to go after signing in', () => {
  it('accepts a path inside the app', () => {
    expect(safeRedirect('/recipes/123')).toBe('/recipes/123');
  });

  it('keeps the query, because that is usually the search that was being done', () => {
    expect(safeRedirect('/recipes?q=soup')).toBe('/recipes?q=soup');
  });

  it.each([
    ['another site outright', 'https://evil.example/steal'],
    ['protocol-relative, which a browser reads as another site', '//evil.example'],
    ['a backslash some parsers normalise into a second slash', '/\\evil.example'],
    ['relative to wherever we happen to be', 'recipes'],
    ['a scheme that is not navigation at all', 'javascript:alert(1)'],
    ['nothing at all', null],
    ['empty', '']
  ])('refuses %s', (_, next) => {
    expect(safeRedirect(next)).toBe('/');
  });
});
