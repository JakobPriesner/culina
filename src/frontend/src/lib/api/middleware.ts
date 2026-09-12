import type { Middleware } from 'openapi-fetch';

import { readCookie } from './cookies';
import { cached, invalidate, remember } from './etagCache';
import { sessionExpired } from './session';

/** The header the backend checks on every unsafe cookie-authenticated request. */
const csrfHeader = 'X-Culina-CSRF';

/** Readable on purpose: echoing it in a header is what an attacker's page cannot do. */
const csrfCookie = 'culina.csrf';

const unsafeMethods = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

const isUnsafe = (request: Request) => unsafeMethods.has(request.method);

/**
 * Proves the request came from our own page.
 *
 * The cookie is read at the moment of sending rather than once at start-up, so
 * a sign-in in another tab is picked up without reloading this one.
 */
export const csrf: Middleware = {
  onRequest({ request }) {
    if (!isUnsafe(request)) {
      return undefined;
    }

    const token = readCookie(csrfCookie);

    if (token) {
      request.headers.set(csrfHeader, token);
    }

    return request;
  }
};

/**
 * Turns a re-read into a 304 and a write into a safe one.
 *
 * Reads carry `If-None-Match` so an unchanged resource costs headers instead of
 * a payload. Writes carry `If-Match` with the version the caller last saw, so
 * two people editing the same recipe get a 412 rather than one silently
 * overwriting the other. A caller that sets `If-Match` itself is left alone.
 */
export const conditionalRequests: Middleware = {
  onRequest({ request }) {
    const entry = cached(request.url);

    if (!entry) {
      return undefined;
    }

    if (request.method === 'GET') {
      request.headers.set('If-None-Match', entry.etag);
    } else if (isUnsafe(request) && !request.headers.has('If-Match')) {
      request.headers.set('If-Match', entry.etag);
    }

    return request;
  },

  async onResponse({ request, response }) {
    if (response.status === 304) {
      const entry = cached(request.url);

      // Without the body we cached, a 304 is unusable — ask again for the
      // whole thing rather than handing the caller an empty success.
      return entry ? replay(entry.body) : undefined;
    }

    if (!response.ok) {
      return undefined;
    }

    if (isUnsafe(request)) {
      invalidate(request.url);

      return undefined;
    }

    const etag = response.headers.get('ETag');

    if (request.method === 'GET' && etag) {
      remember(request.url, etag, await response.clone().text());
    }

    return undefined;
  }
};

/**
 * Ends the session the moment the server says it is over.
 *
 * Never a retry: if the cookie is gone, sending the same request again only
 * produces the same 401, and a loop of them is how a sign-in page ends up
 * flickering instead of appearing.
 */
export const expiredSessions: Middleware = {
  onResponse({ response }) {
    if (response.status === 401) {
      sessionExpired();
    }

    return undefined;
  }
};

const replay = (body: string) =>
  new Response(body, {
    status: 200,
    headers: { 'Content-Type': 'application/json' }
  });
