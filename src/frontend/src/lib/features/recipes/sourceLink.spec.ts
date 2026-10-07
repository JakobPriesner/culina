import { describe, expect, it } from 'vitest';

import { sourceLink } from './sourceLink';

/** The "from chefkoch.de" link: the address is untrusted and shown on the public share page, so only a web address becomes a link, labelled with the host it goes to. */
describe('a link to where a recipe came from', () => {
  it('is the address and its host for an ordinary web page', () => {
    expect(sourceLink('https://www.chefkoch.de/rezepte/123/beans.html')).toEqual({
      href: 'https://www.chefkoch.de/rezepte/123/beans.html',
      host: 'chefkoch.de'
    });
    expect(sourceLink('http://tandoor.lan:8080/view/recipe/7')).toEqual({
      href: 'http://tandoor.lan:8080/view/recipe/7',
      host: 'tandoor.lan:8080'
    });
  });

  it.each([
    'javascript:alert(1)',
    'javascript://chefkoch.de/%0aalert(1)',
    'JAVASCRIPT://chefkoch.de/%0aalert(1)',
    'data:text/html,<script>alert(1)</script>',
    'search-ms:query=recipes',
    'ms-officecmd:{}',
    'file:///etc/passwd',
    'ftp://example.com/recipe',
    'mailto:chef@example.com',
    '/recipes/7',
    'chefkoch.de/rezepte/123',
    'not an address',
    ''
  ])('is nothing for %s', (address) => {
    expect(sourceLink(address)).toBeNull();
  });

  it('is nothing when there is no address', () => {
    expect(sourceLink(null)).toBeNull();
    expect(sourceLink(undefined)).toBeNull();
  });

  it('labels with the host the link actually goes to', () => {
    expect(sourceLink('https://chefkoch.de@evil.example/x')?.host).toBe('evil.example');
  });
});
