/**
 * The routes a search engine may index, in one place.
 *
 * `robots.txt` and `sitemap.xml` are both generated from this, so a new public
 * route cannot end up allowed in one and missing from the other.
 *
 * This is **not access control.** Every route not listed here is protected on
 * the server; robots.txt only asks well-behaved crawlers not to fetch them, and
 * a crawler that ignores it still gets a 401.
 */
export const publicRoutes = ['/', '/login'] as const;

/** Route prefixes that must never be crawled, listed for robots.txt. */
export const privateRoutePrefixes = ['/api/', '/recipes/', '/shopping', '/me'] as const;
