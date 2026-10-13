import { describe, expect, it, vi } from 'vitest';

vi.mock('./platform', () => ({ native: true, nativePlatform: () => 'ios' }));

const { bearerToken, instanceOrigin, loadInstance } = await import('./instance');
const { useSecretStore } = await import('./secrets');

/* A launch of the installed app: the server's address is in device storage, the session token in the Keychain. */

describe('a cold start', () => {
  it('finds the session token even when the API is asked before the Keychain is registered', async () => {
    localStorage.setItem('culina.instance', 'https://kitchen.example.com');

    // Telemetry asks the API from the client hook, before the first route registers the store.
    const early = loadInstance();
    let settled = false;
    void early.then(() => (settled = true));
    await Promise.resolve();

    expect(settled).toBe(false);

    useSecretStore({
      read: (key) => Promise.resolve(key === 'culina.session' ? 'token-1' : null),
      write: () => Promise.resolve(),
      remove: () => Promise.resolve()
    });
    await early;

    expect(instanceOrigin()).toBe('https://kitchen.example.com');
    expect(bearerToken()).toBe('token-1');
  });
});
