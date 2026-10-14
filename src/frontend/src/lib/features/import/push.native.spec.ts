import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('$lib/api/platform', () => ({
  native: true,
  relayUrl: 'https://relay.example.com',
  nativePlatform: () => 'ios'
}));

const store = vi.hoisted(() => ({
  requestDeviceToken: vi.fn(() => Promise.resolve<string | null>('apns-1')),
  notificationsAllowed: vi.fn(() => Promise.resolve(true)),
  stopDeviceToken: vi.fn(() => Promise.resolve()),
  openTappedNotifications: vi.fn(() => Promise.resolve())
}));

vi.mock('./nativePush', () => store);

/* The installed app's "tell me when it is ready": the store's token, the relay's token for it, and the server told. */

type Call = { method: string; url: string; body: unknown };

let relayAvailable = true;
let calls: Call[] = [];

function serve() {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request | URL | string, init?: RequestInit) => {
      const request = input instanceof Request ? input : new Request(String(input), init);
      const text = await request.clone().text();
      const url = new URL(request.url);
      calls.push({ method: request.method, url: url.href, body: text ? JSON.parse(text) : null });

      const json = (body: unknown, status = 200) =>
        new Response(JSON.stringify(body), {
          status,
          headers: { 'Content-Type': 'application/json' }
        });

      if (url.hostname === 'relay.example.com') {
        return request.method === 'POST'
          ? json({ relayToken: 'relay-1' }, 201)
          : new Response(null, { status: 204 });
      }

      if (url.pathname === '/api/v1/push/relay') return json({ available: relayAvailable });

      return new Response(null, { status: 204 });
    })
  );
}

beforeEach(async () => {
  vi.resetModules();
  localStorage.clear();
  relayAvailable = true;
  calls = [];
  vi.clearAllMocks();
  serve();
  localStorage.setItem('culina.instance', 'https://kitchen.example.com');
});

/** A launch: the shell hands the API its keychain before anything asks the server. */
async function load() {
  const { useSecretStore } = await import('$api');
  useSecretStore({
    read: () => Promise.resolve('session-1'),
    write: () => Promise.resolve(),
    remove: () => Promise.resolve()
  });

  return (await import('./push.svelte')).importPush;
}

describe('import notifications in the app', () => {
  it('are offered only when the server names a relay', async () => {
    relayAvailable = false;
    const push = await load();

    await push.restore();

    expect(push.supported).toBe(false);
  });

  it('register the store token at the relay, then the relay token with the server, from one tap', async () => {
    const push = await load();
    await push.restore();

    await push.enable();

    expect(push.enabled).toBe(true);
    const relay = calls.find((call) => call.url === 'https://relay.example.com/v1/devices')!;
    expect(relay.body).toEqual({ platform: 'ios', deviceToken: 'apns-1' });
    const server = calls.find((call) => call.method === 'PUT')!;
    expect(server.url).toMatch(
      /^https:\/\/kitchen\.example\.com\/api\/v1\/push\/devices\/[0-9a-f-]{36}$/
    );
    expect(server.body).toMatchObject({ relayToken: 'relay-1' });
  });

  it('say nothing to the relay or the server when the person refuses', async () => {
    store.requestDeviceToken.mockResolvedValueOnce(null);
    const push = await load();
    await push.restore();

    await push.enable();

    expect(push.enabled).toBe(false);
    expect(push.failed).toBe(true);
    expect(calls.filter((call) => call.method !== 'GET')).toEqual([]);
  });

  it('are forgotten by the server and the relay when turned off, as on signing out', async () => {
    const push = await load();
    await push.restore();
    await push.enable();
    const device = calls.find((call) => call.method === 'PUT')!.url;
    calls = [];

    await push.disable();

    expect(push.enabled).toBe(false);
    expect(calls.map((call) => `${call.method} ${call.url}`)).toEqual([
      `DELETE ${device}`,
      'DELETE https://relay.example.com/v1/devices/relay-1'
    ]);
    expect(store.stopDeviceToken).toHaveBeenCalled();
  });

  it('come back on the next launch while the phone still allows them, keeping the same device', async () => {
    const first = await load();
    await first.restore();
    await first.enable();
    const device = calls.find((call) => call.method === 'PUT')!.url;
    calls = [];
    vi.resetModules();

    const next = await load();
    await next.restore();

    expect(next.enabled).toBe(true);
    expect(calls.find((call) => call.method === 'PUT')!.url).toBe(device);
    expect(store.requestDeviceToken).toHaveBeenCalledTimes(1);
  });
});
