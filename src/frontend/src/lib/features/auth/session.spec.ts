import { beforeEach, describe, expect, it, vi } from 'vitest';

import { registerStore, resetAllStores } from '$shell/stores';

import { session } from './session.svelte';

/*
 * Two things matter here and both are security properties rather than
 * conveniences: the app must know whether anyone is signed in before it renders
 * anything, and signing out must leave nothing behind.
 */
const me = {
  userId: 'u1',
  email: 'jakob@example.com',
  displayName: 'Jakob',
  isAdmin: true,
  createdAt: '2026-01-01T00:00:00Z',
  version: 1,
  households: [
    { householdId: 'h1', name: 'Home', role: 'owner' },
    { householdId: 'h2', name: 'Allotment', role: 'member' }
  ]
};

const settings = {
  locale: 'en',
  theme: 'warm-paper',
  mode: 'light',
  measurementSystem: 'metric',
  version: 1
};

let send: ReturnType<typeof vi.fn>;

function serverAnswers(reply: (url: string, method: string) => Response) {
  send = vi.fn((input: Request) => Promise.resolve(reply(input.url, input.method)));
  vi.stubGlobal('fetch', send);
}

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' }
  });

const signedIn = (url: string) => (url.endsWith('/settings') ? json(settings) : json(me));

const signedOut = () => json({ code: 'auth.not_authenticated', detail: 'Sign in.' }, 401);

beforeEach(() => {
  localStorage.clear();
  resetAllStores();
  serverAnswers(signedIn);
});

describe('resolving the session', () => {
  it('reports who is signed in', async () => {
    await session.resolve();

    expect(session.status).toBe('authenticated');
    expect(session.user?.displayName).toBe('Jakob');
  });

  it('reports a stranger as anonymous rather than as an error', async () => {
    serverAnswers(signedOut);

    await session.resolve();

    expect(session.status).toBe('anonymous');
    expect(session.user).toBeNull();
  });

  it('says so when it could not ask, rather than reporting nobody is signed in', async () => {
    // A backend restarting, a proxy answering 502, a request that timed out.
    // None of them is the server saying there is no session, and treating them
    // as one is what puts a sign-in form in front of a valid cookie.
    serverAnswers(() => json({ code: 'server.unavailable', detail: 'Later.' }, 503));

    await session.resolve();

    expect(session.status).toBe('unavailable');
  });

  it('says so when the request never reached a server', async () => {
    serverAnswers(() => {
      throw new TypeError('Failed to fetch');
    });

    await session.resolve();

    expect(session.status).toBe('unavailable');
  });

  it('treats only a 401 as not being signed in', async () => {
    serverAnswers(signedOut);

    await session.resolve();

    expect(session.status).toBe('anonymous');
  });

  it('remembers which boot screen this device should paint next time', async () => {
    await session.resolve();

    // Read by the inline script in app.html, long before any of this exists.
    expect(localStorage.getItem('culina.boot')).toBe('app');

    serverAnswers(signedOut);
    await session.refresh();

    expect(localStorage.getItem('culina.boot')).toBe('auth');
  });

  it('leaves the boot hint alone when it could not ask', async () => {
    await session.resolve();

    serverAnswers(() => json({ code: 'server.unavailable', detail: 'Later.' }, 503));
    await session.refresh();

    // Guessing "signed out" from a failure would have this device open on a
    // sign-in skeleton for a session that is perfectly good.
    expect(localStorage.getItem('culina.boot')).toBe('app');
  });

  it('asks once, however many callers want the answer', async () => {
    await Promise.all([session.resolve(), session.resolve(), session.resolve()]);

    // Two requests, because the user and the settings are fetched in parallel;
    // what must not happen is three boots asking six times.
    expect(send).toHaveBeenCalledTimes(2);
  });

  it('does not ask again once it has an answer', async () => {
    await session.resolve();
    send.mockClear();

    // Every navigation resolves the session, and a hover preloads one. Asking
    // the server who is signed in on each of them is two requests for an answer
    // that cannot have changed.
    await session.resolve();

    expect(send).not.toHaveBeenCalled();
  });

  it('asks again while it still could not ask', async () => {
    serverAnswers(() => json({ code: 'server.unavailable', detail: 'Later.' }, 503));
    await session.resolve();

    serverAnswers(signedIn);
    await session.resolve();

    expect(session.status).toBe('authenticated');
  });
});

describe('the active household', () => {
  it('is the first one, so nobody is asked a question on boot', async () => {
    await session.resolve();

    expect(session.activeHouseholdId).toBe('h1');
  });

  it('is the one they were last looking at', async () => {
    localStorage.setItem('culina.household', 'h2');

    await session.resolve();

    expect(session.activeHouseholdId).toBe('h2');
  });

  it('falls back when they are no longer a member of the remembered one', async () => {
    localStorage.setItem('culina.household', 'gone');

    await session.resolve();

    expect(session.activeHouseholdId).toBe('h1');
  });

  it('ignores a household they do not belong to', async () => {
    await session.resolve();

    session.selectHousehold('somebody-elses');

    expect(session.activeHouseholdId).toBe('h1');
  });

  it('remembers the choice for the next boot', async () => {
    await session.resolve();

    session.selectHousehold('h2');

    expect(localStorage.getItem('culina.household')).toBe('h2');
  });
});

describe('signing out', () => {
  it('clears every registered store, not only this one', async () => {
    const other = vi.fn();

    registerStore(other);

    await session.resolve();
    await session.signOut();

    expect(other).toHaveBeenCalled();
    expect(session.user).toBeNull();
    expect(session.status).toBe('anonymous');
  });

  it('clears even when the server never answered', async () => {
    await session.resolve();

    serverAnswers(() => {
      throw new TypeError('Failed to fetch');
    });

    await session.signOut();

    // A failed sign-out that leaves the previous person's data on screen is
    // worse than one that ends the session locally.
    expect(session.user).toBeNull();
    expect(session.status).toBe('anonymous');
  });
});

describe('signing in', () => {
  it('returns the failure rather than throwing', async () => {
    serverAnswers((url) =>
      url.endsWith('/sessions')
        ? json({ code: 'auth.invalid_credentials', detail: 'No match.' }, 401)
        : signedOut()
    );

    const error = await session.signIn('jakob@example.com', 'wrong');

    expect(error?.code).toBe('auth.invalid_credentials');
    expect(session.status).not.toBe('authenticated');
  });

  it('reads the session afterwards, because sign-in does not return households', async () => {
    serverAnswers((url, method) =>
      url.endsWith('/sessions') && method === 'POST'
        ? json(
            { userId: 'u1', displayName: 'Jakob', email: 'j@e.com', isAdmin: true, csrfToken: 't' },
            201
          )
        : signedIn(url)
    );

    const error = await session.signIn('jakob@example.com', 'right');

    expect(error).toBeNull();
    expect(session.households).toHaveLength(2);
  });
});

describe('households that inherit recipes', () => {
  const inheriting = {
    ...me,
    households: [
      { householdId: 'h1', name: 'Home', role: 'owner', inheritsFrom: [] },
      {
        householdId: 'h2',
        name: 'Allotment',
        role: 'member',
        inheritsFrom: [
          { householdId: 'h1', name: 'Home' },
          { householdId: 'p1', name: 'Grandma' }
        ]
      }
    ]
  };

  it('names what the household on screen inherits from, and only that', async () => {
    serverAnswers((url) => (url.endsWith('/settings') ? json(settings) : json(inheriting)));
    await session.refresh();

    session.selectHousehold('h2');

    expect(session.inheritedFrom).toEqual({ h1: 'Home', p1: 'Grandma' });
    // Grandma's kitchen is not one this person is in, and is still named:
    // their library is full of its recipes.
    expect(session.householdName('p1')).toBe('Grandma');
    expect(session.householdName('elsewhere')).toBeNull();

    session.selectHousehold('h1');

    expect(session.inheritedFrom).toEqual({});
    expect(session.householdName('p1')).toBeNull();
  });

  it('treats an answer from before households could inherit as inheriting nothing', async () => {
    // The offline copy of /users/me outlives a deploy, so one without the
    // chain can still be what a device boots from.
    await session.refresh();

    expect(session.inheritedFrom).toEqual({});
    expect(session.households.every((h) => h.inheritsFrom.length === 0)).toBe(true);
  });
});
