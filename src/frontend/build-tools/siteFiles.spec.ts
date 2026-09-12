import { describe, expect, it } from 'vitest';

import { robots, securityTxt, sitemap } from './siteFiles';
import { privateRoutePrefixes, publicRoutes } from '../src/lib/app/publicRoutes';

/*
 * Three small public files, each one a thing that is embarrassing to get wrong
 * and impossible to notice: nobody reads robots.txt again after writing it.
 */
const site = 'https://culina.example.com';

describe('robots.txt', () => {
  it('keeps crawlers out of every route behind a sign-in', () => {
    const contents = robots(site);

    for (const prefix of privateRoutePrefixes) {
      expect(contents).toContain(`Disallow: ${prefix}`);
    }
  });

  it('lets them have the routes anyone can see', () => {
    const contents = robots(site);

    for (const route of publicRoutes) {
      expect(contents).toContain(`Allow: ${route}`);
    }
  });

  it('points at the sitemap when there is an address to point with', () => {
    expect(robots(site)).toContain(`Sitemap: ${site}/sitemap.xml`);
  });

  it('says nothing about a sitemap on an instance with no public address', () => {
    expect(robots('')).not.toContain('Sitemap:');
  });
});

describe('sitemap.xml', () => {
  it('lists exactly the public routes', () => {
    const contents = sitemap(site);

    for (const route of publicRoutes) {
      expect(contents).toContain(`<loc>${site}${route}</loc>`);
    }

    expect(contents.match(/<loc>/g)).toHaveLength(publicRoutes.length);
  });

  it('never lists a protected one', () => {
    expect(sitemap(site)).not.toContain('/me');
  });
});

describe('security.txt', () => {
  it('expires in the future, because an expired one is worse than none', () => {
    const contents = securityTxt(
      site,
      'mailto:security@example.com',
      new Date('2026-09-12T00:00:00Z')
    );

    expect(contents).toContain('Expires: 2027-09-12T00:00:00Z');
  });

  it('carries the contact it was given, not a guess', () => {
    expect(securityTxt(site, 'mailto:security@example.com')).toContain(
      'Contact: mailto:security@example.com'
    );
  });

  it('names both languages a report may be written in', () => {
    expect(securityTxt(site, 'mailto:a@b.c')).toContain('Preferred-Languages: en, de');
  });
});
