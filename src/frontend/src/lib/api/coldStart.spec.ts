import { describe, expect, it, vi } from 'vitest';

vi.mock('./platform', () => ({ native: true, nativePlatform: () => 'ios' }));

const sent: Request[] = [];

vi.mock('./transport', () => ({
  send: (request: Request) => {
    sent.push(request);

    return Promise.resolve(
      new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } })
    );
  },
  forgetOfflineReads: () => Promise.resolve()
}));

const { http, request } = await import('./client');
const { useSecretStore } = await import('./secrets');

/* The installed app's first launch of the day: the session load runs beside the root layout's, before the token is read. */

describe('the first request of a cold start', () => {
  it('carries the session token, though it was made before the token was out of the Keychain', async () => {
    localStorage.setItem('culina.instance', 'https://kitchen.example.com');

    const first = request(() => http.GET('/api/v1/users/me'));

    useSecretStore({
      read: (key) => Promise.resolve(key === 'culina.session' ? 'token-1' : null),
      write: () => Promise.resolve(),
      remove: () => Promise.resolve()
    });
    await first;

    expect(sent).toHaveLength(1);
    expect(sent[0]!.url).toBe('https://kitchen.example.com/api/v1/users/me');
    expect(sent[0]!.headers.get('Authorization')).toBe('Bearer token-1');
  });
});
