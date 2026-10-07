import type { Middleware } from 'openapi-fetch';

import { csrfToken } from './cookies';
import { cached, invalidate, remember } from './etagCache';
import { ErrorCodes } from './problem';
import { sessionExpired } from './session';

const csrfHeader = 'X-Culina-CSRF';

const unsafeMethods = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

const isUnsafe = (request: Request) => unsafeMethods.has(request.method);

/**
 * Proves the request came from our own page; the cookie is read at send time so a sign-in in
 * another tab is picked up.
 */
export const csrf: Middleware = {
  onRequest({ request }) {
    if (!isUnsafe(request)) {
      return undefined;
    }

    const token = csrfToken();

    if (token) {
      request.headers.set(csrfHeader, token);
    }

    return request;
  }
};

/**
 * Turns a re-read into a 304 and a write into a safe one: `If-None-Match` on reads, `If-Match` on
 * writes (412 on a lost race); a caller's own `If-Match` is left alone.
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

      // A 304 without our cached body is unusable: ask again for the whole thing.
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
 * Ends the session the moment the server says it is over; never a retry (the same 401 would loop).
 * A wrong password is also a 401 but belongs to the sign-in form, not a session ending, or each
 * typo would wipe drafts and nest `next`.
 */
export const expiredSessions: Middleware = {
  async onResponse({ response }) {
    if (response.status === 401 && (await codeOf(response)) !== ErrorCodes.invalidCredentials) {
      sessionExpired();
    }

    return undefined;
  }
};

async function codeOf(response: Response): Promise<unknown> {
  try {
    const body: unknown = await response.clone().json();

    return typeof body === 'object' && body !== null && 'code' in body ? body.code : undefined;
  } catch {
    return undefined;
  }
}

const replay = (body: string) =>
  new Response(body, {
    status: 200,
    headers: { 'Content-Type': 'application/json' }
  });
