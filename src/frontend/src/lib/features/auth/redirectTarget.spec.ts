import { describe, expect, it } from 'vitest';

import { loginUrlFor, safeRedirect } from './redirectTarget';

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

describe('the sign-in address', () => {
  it('remembers where somebody was going', () => {
    expect(loginUrlFor(new URL('http://culina.test/recipes?q=soup'))).toBe(
      '/login?next=%2Frecipes%3Fq%3Dsoup'
    );
  });

  it('says the session ended, so the sign-in page can explain the bounce', () => {
    expect(loginUrlFor(new URL('http://culina.test/shopping'), 'expired')).toBe(
      '/login?next=%2Fshopping&reason=expired'
    );
  });

  it('keeps where somebody was going when they are already signing in', () => {
    expect(loginUrlFor(new URL('http://culina.test/login?next=%2Frecipes%2Fx'), 'expired')).toBe(
      '/login?next=%2Frecipes%2Fx&reason=expired'
    );
  });
});
