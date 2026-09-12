import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import type { Plugin } from 'vite';

import { privateRoutePrefixes, publicRoutes } from '../src/lib/app/publicRoutes';

/**
 * Writes the three small public files that are easy to forget and awkward to
 * get wrong by hand.
 *
 * Generated rather than committed because each one repeats something that lives
 * elsewhere — the list of public routes, and the site's own address — and a
 * copy that has to be updated by hand is a copy that will be stale.
 *
 * `PUBLIC_SITE_URL` is the deployment's own address. Without it the files that
 * need an absolute URL are skipped: a self-hosted instance on a private network
 * has no public address and should not invent one.
 *
 * `PUBLIC_SECURITY_CONTACT` is where a vulnerability report should go — a
 * mailto: or a URL the operator actually reads. Without it there is no
 * security.txt at all, because a contact nobody reads is worse than the absence
 * of one: it tells a finder they have reported something when they have not.
 */
export function siteFiles(): Plugin {
  return {
    name: 'culina:site-files',
    apply: 'build',

    // After the adapter, which creates `build/` as its last act: writing into
    // that directory any earlier means writing into something that is about to
    // be replaced.
    closeBundle: {
      sequential: true,
      order: 'post',

      async handler() {
        const site = trimSlash(process.env['PUBLIC_SITE_URL'] ?? '');
        const contact = process.env['PUBLIC_SECURITY_CONTACT'] ?? '';
        const outDir = 'build';

        await write(outDir, 'robots.txt', robots(site));

        if (site) {
          await write(outDir, 'sitemap.xml', sitemap(site));
        }

        if (site && contact) {
          await write(outDir, '.well-known/security.txt', securityTxt(site, contact));
        }
      }
    }
  };
}

const trimSlash = (value: string) => value.replace(/\/+$/, '');

/**
 * Asks crawlers to stay out of everything behind a sign-in.
 *
 * Not a security measure — every one of these returns 401 to a stranger — but
 * an indexed URL to a household's recipe list is a leak of the fact that it
 * exists, and of nothing being served there but a sign-in page.
 */
export function robots(site: string): string {
  const lines = [
    'User-agent: *',
    ...publicRoutes.map((route) => `Allow: ${route}`),
    ...privateRoutePrefixes.map((prefix) => `Disallow: ${prefix}`)
  ];

  if (site) {
    lines.push('', `Sitemap: ${site}/sitemap.xml`);
  }

  return `${lines.join('\n')}\n`;
}

export function sitemap(site: string): string {
  const urls = publicRoutes
    .map((route) => `  <url>\n    <loc>${site}${route === '/' ? '/' : route}</loc>\n  </url>`)
    .join('\n');

  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${urls}
</urlset>
`;
}

/**
 * RFC 9116. `Expires` is a year out from the build, which is the honest value:
 * an expired security.txt is worse than none, and the thing that keeps it fresh
 * is that every release regenerates it.
 */
export function securityTxt(site: string, contact: string, now = new Date()): string {
  const expires = new Date(now);

  expires.setUTCFullYear(expires.getUTCFullYear() + 1);
  expires.setUTCMilliseconds(0);

  return [
    `Contact: ${contact}`,
    `Expires: ${expires.toISOString().replace(/\.\d{3}Z$/, 'Z')}`,
    'Preferred-Languages: en, de',
    `Canonical: ${site}/.well-known/security.txt`,
    ''
  ].join('\n');
}

async function write(outDir: string, path: string, contents: string): Promise<void> {
  const target = join(outDir, path);

  await mkdir(dirname(target), { recursive: true });
  await writeFile(target, contents, 'utf8');
}
