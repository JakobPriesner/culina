import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('./platform', () => ({ native: true, nativePlatform: () => 'ios' }));

const { connect, disconnect, normaliseAddress } = await import('./connect');
const { apiUrl, bearerToken, instanceOrigin, publicOrigin, rememberSessionToken } =
  await import('./instance');
const { authorization, credentialsMode } = await import('./credentials');

/* The app's first screen: an address becomes a server only if a Culina this build understands answers there. */

const setup = (body: unknown, status = 200) =>
  vi.fn(() =>
    Promise.resolve(
      new Response(JSON.stringify(body), {
        status,
        headers: { 'Content-Type': 'application/json' }
      })
    )
  );

beforeEach(async () => {
  await disconnect();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('reading a typed address', () => {
  it('takes a bare host as https and keeps only the origin', () => {
    expect(normaliseAddress(' kitchen.example.com/recipes ')).toBe('https://kitchen.example.com');
    expect(normaliseAddress('https://kitchen.example.com:8443/x')).toBe(
      'https://kitchen.example.com:8443'
    );
  });

  it('allows plain http only on the home network, where a password never crosses the internet', () => {
    expect(normaliseAddress('http://192.168.1.20:8080')).toBe('http://192.168.1.20:8080');
    expect(normaliseAddress('http://culina.local')).toBe('http://culina.local');
    expect(normaliseAddress('http://kitchen.example.com')).toBeNull();
  });

  it('refuses what is not an address', () => {
    expect(normaliseAddress('')).toBeNull();
    expect(normaliseAddress('ftp://kitchen.example.com')).toBeNull();
    expect(normaliseAddress('https://ada:secret@kitchen.example.com')).toBeNull();
  });
});

describe('connecting', () => {
  it('makes the server the instance when a set-up Culina it understands answers', async () => {
    const fetch = setup({ stage: 'complete', startedAt: '2026-10-12T00:00:00Z', apiVersion: 1 });
    vi.stubGlobal('fetch', fetch);

    expect(await connect('kitchen.example.com')).toBeNull();
    expect(fetch).toHaveBeenCalledWith(
      'https://kitchen.example.com/api/v1/setup',
      expect.anything()
    );
    expect(instanceOrigin()).toBe('https://kitchen.example.com');
    expect(apiUrl('/api/v1/users/me')).toBe('https://kitchen.example.com/api/v1/users/me');
    expect(publicOrigin()).toBe('https://kitchen.example.com');
  });

  it('says why it could not', async () => {
    vi.stubGlobal('fetch', setup({ stage: 'account', apiVersion: 1 }));
    expect(await connect('kitchen.example.com')).toBe('not-set-up');

    vi.stubGlobal('fetch', setup({ stage: 'complete' }));
    expect(await connect('kitchen.example.com')).toBe('server-too-old');

    vi.stubGlobal('fetch', setup({ stage: 'complete', apiVersion: 99 }));
    expect(await connect('kitchen.example.com')).toBe('app-too-old');

    vi.stubGlobal('fetch', setup('<html>', 404));
    expect(await connect('kitchen.example.com')).toBe('not-culina');

    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.reject(new TypeError('Failed to fetch')))
    );
    expect(await connect('kitchen.example.com')).toBe('unreachable');

    expect(instanceOrigin()).toBeNull();
  });

  it('starts every new server signed out', async () => {
    vi.stubGlobal('fetch', setup({ stage: 'complete', apiVersion: 1 }));
    await connect('one.example.com');
    await rememberSessionToken('token-for-one');

    await connect('two.example.com');

    expect(bearerToken()).toBeNull();
  });
});

describe('signing requests in the app', () => {
  it('sends the bearer token and no cookies, and never a CSRF token', async () => {
    await rememberSessionToken('abc');

    expect(credentialsMode).toBe('omit');
    expect(authorization(true)).toEqual({ Authorization: 'Bearer abc' });
    expect(authorization(false)).toEqual({ Authorization: 'Bearer abc' });
  });
});
