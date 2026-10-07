/**
 * The routes a search engine may index, the one source for robots.txt and sitemap.xml.
 * Not access control: unlisted routes are protected on the server; robots.txt only asks well-behaved crawlers.
 */
export const publicRoutes = ['/', '/login'] as const;

/** Prefixes robots.txt keeps crawlers out of; `/shared/` needs no session, but a share link is sent to one person and must not reach a search index. */
export const privateRoutePrefixes = ['/api/', '/recipes/', '/shared/', '/shopping', '/me'] as const;
