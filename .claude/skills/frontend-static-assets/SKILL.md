---
name: frontend-static-assets
description: The static files the culina-v2 frontend must ship and keep correct — robots.txt, sitemap.xml, security.txt, the web manifest, icons, favicons and humans.txt — plus how they are served and cached. Use when setting up the frontend, adding a public route, changing the app name or icons, or preparing for deployment.
---

# Static files

Files in `src/frontend/static/` are copied to the build root verbatim;
`robots.txt` and `sitemap.xml` are generated into `build/` by
`build-tools/siteFiles.ts`, and `security.txt` is written by the backend.
These files are small, easy to forget, and each one is either a
security, SEO, or install-experience requirement.

## robots.txt

Culina is a self-hosted, mostly-authenticated app: the crawl surface is the
marketing/landing and documentation routes, and nothing else.

Generated from `publicRoutes` and `privateRoutePrefixes` in
`src/lib/app/publicRoutes.ts`: one `Allow` per public route, one `Disallow` per
private prefix. A new route is added to that file, never to a copy of the list.

- Authenticated routes are disallowed because a crawler that follows a shared
  link should not index a private page's shell.
- `robots.txt` is **not** an access control. Every route it disallows must
  still be protected server-side.
- The absolute `Sitemap:` URL is the one thing here that needs the deployment's
  origin, so it comes from `PUBLIC_SITE_URL` at build time and is left out
  when that is unset.

## sitemap.xml

Only the routes a crawler may see — typically the landing page, login and any
public documentation. It is generated at build time from the list of public
routes, never hand-maintained, so a new public route cannot be forgotten:

```xml
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url><loc>https://example.com/</loc><changefreq>monthly</changefreq></url>
  <url><loc>https://example.com/login</loc><changefreq>yearly</changefreq></url>
</urlset>
```

Without `PUBLIC_SITE_URL` no sitemap is written: a private instance has no
public address to put in one.

## security.txt

At **`/.well-known/security.txt`** (RFC 9116), and **not a static file**: the
contact belongs to whoever runs the instance, and a published image is the same
bytes for everyone. The backend writes it per request from `Site__SecurityContact`
and `Site__Url` (`Api/Endpoints/WellKnown/SecurityTxtEndpoint`), and answers
`404` when no contact is configured.

```
Contact: mailto:security@example.com
Expires: 2027-04-04T12:00:00Z
Preferred-Languages: en, de
Canonical: https://example.com/.well-known/security.txt
```

`Expires` is mandatory and must be in the future — an expired `security.txt`
is worse than none. Written per request, it is always half a year out.

## Web manifest and icons

`static/manifest.webmanifest`, referenced from `app.html`:

```json
{
  "name": "Culina",
  "short_name": "Culina",
  "start_url": "/",
  "scope": "/",
  "display": "standalone",
  "background_color": "#ffffff",
  "theme_color": "#1f2937",
  "icons": [
    { "src": "/icon-192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "/icon-512.png", "sizes": "512x512", "type": "image/png" },
    { "src": "/icon-maskable-512.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable" }
  ]
}
```

Also ship: `favicon.ico` (or `icon.svg` plus a PNG fallback),
`apple-touch-icon.png` (180×180), and a maskable icon. `name`, `theme_color`
and the icons must match the app's actual branding — a mismatched install
prompt looks broken.

## Serving and caching

- Hashed build assets (`/_app/immutable/...`) →
  `Cache-Control: public, max-age=31536000, immutable`.
- Unhashed static files (`robots.txt`, `manifest.webmanifest`, icons) →
  a short `max-age` with revalidation (e.g. `public, max-age=3600`), so a fix
  reaches clients the same day.
- `index.html` / the SPA shell → `no-cache`: it must always revalidate, or a
  deploy never reaches anyone.
- These files are public by definition. Nothing here may contain an internal
  hostname, an email that is not meant to be public, a token, or a version
  string that reveals more than the release does.
- The security headers in `cookie-auth-and-security` apply to these responses
  too; the SPA document's CSP is set by the backend that serves it.

## Checklist

- [ ] `robots.txt` disallows every authenticated route and names the sitemap.
- [ ] `sitemap.xml` is build-generated from the public route list.
- [ ] `/.well-known/security.txt` is served by the backend from
      `Site__SecurityContact`, with a future `Expires`, and never shadowed by a
      static file.
- [ ] `manifest.webmanifest` + 192/512/maskable icons + favicon +
      apple-touch-icon, matching the real branding.
- [ ] Cache headers: immutable for hashed assets, short for static files,
      `no-cache` for the shell.
- [ ] Nothing public leaks internal detail.
