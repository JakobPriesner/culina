import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { http, request } from './client';
import { forgetEverything } from './etagCache';
import { ErrorCodes } from './problem';
import { handleSessionExpiry } from './session';

/* The client alone knows CSRF, conditional requests and failure shape, all invisible at call sites, so each is asserted here. */

const me = '/api/v1/users/me';

let sent: Request[] = [];

/** A reply, or a function making one when the request arrives, for a side effect in between. */
type Reply = Response | (() => Response);

function respondWith(...replies: Reply[]) {
  const queue = [...replies];

  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request) => {
      sent.push(input);

      const next = queue.shift();

      if (!next) {
        return Promise.reject(new TypeError('Failed to fetch'));
      }

      return Promise.resolve(typeof next === 'function' ? next() : next);
    })
  );
}

const json = (body: unknown, init: ResponseInit = {}) =>
  new Response(JSON.stringify(body), {
    status: 200,
    ...init,
    headers: { 'Content-Type': 'application/json', ...init.headers }
  });

/* Captured once at module scope: re-reading in the helper would bind the previous test's spy. */
const realTimeout = AbortSignal.timeout.bind(AbortSignal);

describe('deadlines', () => {
  function watchDeadlines() {
    const asked: number[] = [];

    vi.spyOn(AbortSignal, 'timeout').mockImplementation((ms: number) => {
      asked.push(ms);

      return realTimeout(ms);
    });

    return asked;
  }

  afterEach(() => vi.restoreAllMocks());

  it('gives an ordinary call the short deadline', async () => {
    const asked = watchDeadlines();

    respondWith(json({ userId: 'u1' }));

    await request(() => http.GET('/api/v1/users/me'));

    expect(asked).toEqual([15_000]);
  });

  it('gives a connected-library call a deadline that fits what it does', async () => {
    const asked = watchDeadlines();

    respondWith(json({ cookbookId: 'cb1', cookbookName: 'Tandoor', results: [] }));

    await request(() =>
      http.POST('/api/v1/recipe-sources/{sourceId}/imports', {
        params: { path: { sourceId: 's1' } },
        body: { externalIds: ['1'] }
      })
    );

    // One request is several round trips plus re-encoding; fifteen seconds aborted whole batches.
    expect(asked).toEqual([60_000]);
  });

  it('gives reading a connected library the same longer deadline', async () => {
    const asked = watchDeadlines();

    respondWith(json({ items: [], nextPage: null, total: 0 }));

    await request(() =>
      http.GET('/api/v1/recipe-sources/{sourceId}/recipes', {
        params: { path: { sourceId: 's1' }, query: {} }
      })
    );

    expect(asked).toEqual([60_000]);
  });

  it('keeps the short deadline for sending a photograph, which is not a slow call', async () => {
    const asked = watchDeadlines();

    respondWith(json({ recipeId: 'r1', imageId: 'i3' }));

    const body = new FormData();

    body.append('file', new File(['bytes'], 'dinner.jpg', { type: 'image/jpeg' }));

    await request(() =>
      http.PUT('/api/v1/recipes/{recipeId}/image', {
        params: { path: { recipeId: 'r1' } },
        body: body as unknown as { file: string },
        bodySerializer: (value: unknown) => value as FormData
      })
    );

    // Drawing is not sent through here: asking a model to make bytes is a stream read by `ask` in `./events`, with no deadline.
    expect(asked).toEqual([15_000]);
  });

  it('waits while every connected provider is asked what it offers', async () => {
    const asked = watchDeadlines();

    respondWith(json({ providers: [] }));

    await request(() => http.GET('/api/v1/settings/assistance/models'));

    expect(asked).toEqual([60_000]);
  });
});

const problem = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/problem+json' }
  });

beforeEach(() => {
  sent = [];
  forgetEverything();
  document.cookie = 'culina.csrf=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
  handleSessionExpiry(() => {});
});

describe('a successful read', () => {
  it('returns the body as a value', async () => {
    respondWith(json({ id: 'u1' }));

    const result = await request(() => http.GET(me));

    expect(result).toEqual({ ok: true, value: { id: 'u1' } });
  });

  it('sends no CSRF header, because a read cannot be forged into a change', async () => {
    respondWith(json({ id: 'u1' }));

    await request(() => http.GET(me));

    expect(sent[0]?.headers.get('X-Culina-CSRF')).toBeNull();
  });
});

describe('an unsafe request', () => {
  it('echoes the CSRF cookie in the header the backend checks', async () => {
    document.cookie = 'culina.csrf=token-abc; path=/';
    respondWith(json({ ok: true }, { status: 201 }));

    await request(() => http.POST('/api/v1/sessions', { body: { email: 'a@b.c', password: 'x' } }));

    expect(sent[0]?.headers.get('X-Culina-CSRF')).toBe('token-abc');
  });

  it('reads the cookie at send time, so a sign-in in another tab is picked up', async () => {
    respondWith(json({}, { status: 201 }), json({}, { status: 201 }));

    await request(() => http.POST('/api/v1/sessions', { body: { email: 'a@b.c', password: 'x' } }));

    document.cookie = 'culina.csrf=later; path=/';

    await request(() => http.POST('/api/v1/sessions', { body: { email: 'a@b.c', password: 'x' } }));

    expect(sent[0]?.headers.get('X-Culina-CSRF')).toBeNull();
    expect(sent[1]?.headers.get('X-Culina-CSRF')).toBe('later');
  });
});

describe('conditional requests', () => {
  it('re-reads with If-None-Match and serves a 304 from memory', async () => {
    respondWith(
      json({ id: 'u1' }, { headers: { ETag: '"v3"' } }),
      new Response(null, { status: 304 })
    );

    await request(() => http.GET(me));
    const second = await request(() => http.GET(me));

    expect(sent[1]?.headers.get('If-None-Match')).toBe('"v3"');
    expect(second).toEqual({ ok: true, value: { id: 'u1' } });
  });

  it('sends If-Match on a write, so a stale edit is refused rather than applied', async () => {
    respondWith(json({ mode: 'dark' }, { headers: { ETag: '"v3"' } }), json({ version: 4 }));

    await request(() => http.GET('/api/v1/users/me/settings'));
    await request(() =>
      http.PUT('/api/v1/users/me/settings', {
        body: { locale: 'de', theme: 'warm-paper', mode: 'dark', measurementSystem: 'metric' }
      })
    );

    expect(sent[1]?.headers.get('If-Match')).toBe('"v3"');
  });

  it('forgets what a write could have changed', async () => {
    respondWith(
      json({ id: 'u1' }, { headers: { ETag: '"v3"' } }),
      json({ version: 4 }),
      json({ id: 'u1' })
    );

    await request(() => http.GET(me));
    await request(() =>
      http.PUT('/api/v1/users/me/settings', {
        body: { locale: 'de', theme: 'warm-paper', mode: 'dark', measurementSystem: 'metric' }
      })
    );
    await request(() => http.GET(me));

    expect(sent[2]?.headers.get('If-None-Match')).toBeNull();
  });
});

describe('a failure', () => {
  it('becomes a result carrying the code, the request id and every wrong field', async () => {
    respondWith(
      problem(400, {
        code: 'recipe.invalid',
        detail: 'That recipe is not valid.',
        requestId: 'req-9',
        errors: [{ field: 'title', code: 'required', detail: 'Give it a name.' }]
      })
    );

    const result = await request(() => http.GET(me));

    expect(result).toEqual({
      ok: false,
      error: {
        code: 'recipe.invalid',
        detail: 'That recipe is not valid.',
        status: 400,
        requestId: 'req-9',
        fields: [{ field: 'title', code: 'required', detail: 'Give it a name.' }],
        retryAfterSeconds: null
      }
    });
  });

  it('is still a result when the body is not a problem document', async () => {
    respondWith(new Response('<html>gateway</html>', { status: 502 }));

    const result = await request(() => http.GET(me));

    expect(result.ok).toBe(false);
    expect(result.ok === false && result.error.code).toBe(ErrorCodes.unexpected);
  });

  it('is still a failure when the body is empty', async () => {
    respondWith(
      new Response(null, { status: 403, headers: { 'Content-Length': '0' } }),
      new Response(null, { status: 502, headers: { 'Content-Length': '0' } })
    );

    const forbidden = await request(() => http.GET(me));
    const badGateway = await request(() => http.GET(me));

    expect(forbidden).toMatchObject({
      ok: false,
      error: { code: ErrorCodes.unexpected, status: 403 }
    });
    expect(badGateway).toMatchObject({
      ok: false,
      error: { code: ErrorCodes.unexpected, status: 502 }
    });
  });

  it('is an offline error when the request never reached a server', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.reject(new TypeError('Failed to fetch')))
    );

    const result = await request(() => http.GET(me));

    expect(result.ok === false && result.error.code).toBe(ErrorCodes.offline);
  });

  it('is a timeout when we gave up waiting', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.reject(new DOMException('The operation timed out.', 'TimeoutError')))
    );

    const result = await request(() => http.GET(me));

    expect(result.ok === false && result.error.code).toBe(ErrorCodes.timeout);
  });
});

describe('being rate limited', () => {
  it('carries how long to wait, so the form can say when rather than "later"', async () => {
    respondWith(
      new Response(JSON.stringify({ code: 'request.too_many', detail: 'Slow down.' }), {
        status: 429,
        headers: { 'Content-Type': 'application/problem+json', 'Retry-After': '45' }
      })
    );

    const result = await request(() => http.GET(me));

    expect(result.ok === false && result.error.retryAfterSeconds).toBe(45);
  });

  it('reads an HTTP date as well as a count of seconds', async () => {
    const when = new Date(Date.now() + 90_000).toUTCString();

    respondWith(
      new Response(JSON.stringify({ code: 'request.too_many', detail: 'Slow down.' }), {
        status: 429,
        headers: { 'Content-Type': 'application/problem+json', 'Retry-After': when }
      })
    );

    const result = await request(() => http.GET(me));
    const seconds = result.ok === false ? result.error.retryAfterSeconds : null;

    expect(seconds).toBeGreaterThan(80);
    expect(seconds).toBeLessThanOrEqual(90);
  });

  it('never reports a negative wait, however wrong the clock is', async () => {
    respondWith(
      new Response(JSON.stringify({ code: 'request.too_many', detail: 'Slow down.' }), {
        status: 429,
        headers: {
          'Content-Type': 'application/problem+json',
          'Retry-After': new Date(Date.now() - 60_000).toUTCString()
        }
      })
    );

    const result = await request(() => http.GET(me));

    expect(result.ok === false && result.error.retryAfterSeconds).toBe(0);
  });
});

describe('an expired session', () => {
  it('is reported once and never retried', async () => {
    const expired = vi.fn();

    handleSessionExpiry(expired);
    respondWith(problem(401, { code: ErrorCodes.notAuthenticated, detail: 'Sign in.' }));

    await request(() => http.GET(me));

    expect(expired).toHaveBeenCalledOnce();
    expect(sent).toHaveLength(1);
  });

  it('is not what a wrong password means', async () => {
    const expired = vi.fn();

    handleSessionExpiry(expired);
    respondWith(problem(401, { code: ErrorCodes.invalidCredentials, detail: 'No match.' }));

    const result = await request(() =>
      http.POST('/api/v1/sessions', { body: { email: 'a@b.c', password: 'wrong' } })
    );

    expect(result.ok === false && result.error.code).toBe(ErrorCodes.invalidCredentials);
    expect(expired).not.toHaveBeenCalled();
  });

  it('drops everything read as the previous person', async () => {
    respondWith(
      json({ id: 'u1' }, { headers: { ETag: '"v3"' } }),
      problem(401, { code: ErrorCodes.notAuthenticated, detail: 'Sign in.' }),
      json({ id: 'u2' })
    );

    await request(() => http.GET(me));
    await request(() => http.GET(me));
    await request(() => http.GET(me));

    expect(sent[2]?.headers.get('If-None-Match')).toBeNull();
  });
});

describe('a rejected CSRF token', () => {
  const refused = () => problem(403, { code: ErrorCodes.csrfInvalid, detail: 'Could not verify.' });

  const refusedAfterSignInElsewhere = () => {
    document.cookie = 'culina.csrf=after; path=/';

    return refused();
  };

  const signIn = () =>
    request(() => http.POST('/api/v1/sessions', { body: { email: 'a@b.c', password: 'x' } }));

  beforeEach(() => {
    document.cookie = 'culina.csrf=before; path=/';
  });

  it('is not resent when the cookie is unchanged, because the same token earns the same refusal', async () => {
    respondWith(refused(), json({}, { status: 201 }));

    const result = await signIn();

    expect(sent).toHaveLength(1);
    expect(result.ok === false && result.error.code).toBe(ErrorCodes.csrfInvalid);
  });

  it('is retried once, with the new token, when the cookie changed in the meantime', async () => {
    respondWith(refusedAfterSignInElsewhere, json({}, { status: 201 }));

    const result = await signIn();

    expect(sent).toHaveLength(2);
    expect(sent[1]?.headers.get('X-Culina-CSRF')).toBe('after');
    expect(result.ok).toBe(true);
  });

  it('gives up after the second refusal rather than looping', async () => {
    respondWith(refusedAfterSignInElsewhere, refused());

    const result = await signIn();

    expect(sent).toHaveLength(2);
    expect(result.ok === false && result.error.code).toBe(ErrorCodes.csrfInvalid);
  });
});
