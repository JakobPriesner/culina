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

/**
 * Route prefixes that must never be crawled, listed for robots.txt.
 *
 * `/shared/` is here although it needs no session: a share link is sent to one
 * person, not published, and a crawler that found one in a mail archive would
 * otherwise put somebody's recipe in a search index at an address nobody can
 * guess but everybody can then read.
 */
export const privateRoutePrefixes = ['/api/', '/recipes/', '/shared/', '/shopping', '/me'] as const;
