import { describe, expect, it } from 'vitest';

import { robots, sitemap } from './siteFiles';
import { privateRoutePrefixes, publicRoutes } from '../src/lib/app/publicRoutes.js';

/*
 * Two small public files, each one a thing that is embarrassing to get wrong
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
