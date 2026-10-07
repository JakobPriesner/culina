import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import type { Plugin } from 'vite';

import { privateRoutePrefixes, publicRoutes } from '../src/lib/app/publicRoutes.js';

/**
 * Writes robots.txt and sitemap.xml, generated because they repeat the public-route list and the site address.
 * `PUBLIC_SITE_URL` is the deployment's address; without it the absolute-URL files are skipped. security.txt is the server's (`Site__SecurityContact`).
 */
export function siteFiles(): Plugin {
  return {
    name: 'culina:site-files',
    apply: 'build',

    // After the adapter, which creates `build/` as its last act; earlier would write into a directory about to be replaced.
    closeBundle: {
      sequential: true,
      order: 'post',

      async handler() {
        const site = trimSlash(process.env['PUBLIC_SITE_URL'] ?? '');
        const outDir = 'build';

        await write(outDir, 'robots.txt', robots(site));

        if (site) {
          await write(outDir, 'sitemap.xml', sitemap(site));
        }
      }
    }
  };
}

const trimSlash = (value: string) => value.replace(/\/+$/, '');

/** Asks crawlers to stay out of everything behind a sign-in; not a security measure, but an indexed URL would leak that the household exists. */
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

async function write(outDir: string, path: string, contents: string): Promise<void> {
  const target = join(outDir, path);

  await mkdir(dirname(target), { recursive: true });
  await writeFile(target, contents, 'utf8');
}
