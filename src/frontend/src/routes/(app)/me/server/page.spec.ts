import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import ServerPage from './+page.svelte';
import { server } from '$features/server/stores/server.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * The page, because what matters is what an administrator can and cannot do
 * from it: see what the deployment has fixed, and save a change knowing the
 * server restarts to apply it.
 */
const serverSettings = {
  cookies: { secure: true, sessionDays: 30, renewAfterHours: 24 },
  forwardedHeaders: { knownProxies: [], knownNetworks: [] },
  rateLimits: {
    loginPerIpPerMinute: 10,
    loginPerAccountPerMinute: 5,
    registerPerIpPerHour: 5,
    invitationPerIpPerHour: 10,
    importsPerHour: 30,
    sourceRequestsPerHour: 1500,
    sharedRecipesPerIpPerMinute: 120,
    assistantRequestsPerHour: 60,
    requestsPerSessionPerMinute: 600
  },
  telemetry: { otlpEndpoint: null, otlpProtocol: 'grpc' },
  connection: { remoteAddress: '172.18.0.5', forwarded: true, proxyTrusted: false },
  pinned: ['Cookies__Secure'],
  writable: true
};

const database = {
  host: 'db',
  port: 5432,
  name: 'culina',
  username: 'culina_app',
  passwordConfigured: true,
  requireSsl: false,
  maxPoolSize: 20,
  pinned: [],
  writable: true
};

const me = {
  userId: 'u1',
  email: 'ada@example.com',
  displayName: 'Ada',
  isAdmin: true,
  createdAt: '2026-01-01T00:00:00Z',
  version: 1,
  households: [{ householdId: 'h1', name: 'Home', role: 'owner' }]
};

const json = (body: object, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

interface Answers {
  readonly settings?: object;
  readonly saved?: number;
}

/** Answers every request the page makes; the host "restarts" when a save is accepted. */
function serverAnswers({ settings = serverSettings, saved = 204 }: Answers = {}) {
  let startedAt = '2026-09-24T10:00:00Z';

  const fetched = vi.fn((input: unknown) => {
    const request = input as Request;
    const url = request.url;

    if (request.method === 'PUT') {
      if (saved === 202) startedAt = '2026-09-24T10:00:05Z';

      return Promise.resolve(new Response(null, { status: saved }));
    }

    if (url.includes('/setup')) return Promise.resolve(json({ stage: 'complete', startedAt }));
    if (url.includes('/settings/database')) return Promise.resolve(json(database));
    if (url.includes('/settings/server')) return Promise.resolve(json(settings));
    if (url.includes('/users/me/settings')) {
      return Promise.resolve(
        json({
          locale: 'en',
          theme: 'warm-paper',
          mode: 'light',
          measurementSystem: 'metric',
          version: 1
        })
      );
    }

    return Promise.resolve(json(me));
  });

  vi.stubGlobal('fetch', fetched);

  return fetched;
}

const settle = async () => {
  for (let turn = 0; turn < 5; turn += 1) {
    await new Promise((resume) => setTimeout(resume, 0));
  }
};

beforeEach(() => {
  server.clear();
});

describe('the server settings page', () => {
  it('shows a setting the environment fixes as fixed, naming the variable', async () => {
    serverAnswers();

    renderWithProviders(ServerPage);
    await settle();

    // Editable, it would accept a change the next start ignores.
    expect(screen.getByRole('switch', { name: 'Secure cookies' })).toBeDisabled();
    expect(screen.getByText(/Set by Cookies__Secure/)).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Days a session lasts' })).toBeEnabled();
  });

  it('offers to trust the proxy the request came through', async () => {
    serverAnswers();

    renderWithProviders(ServerPage);
    await settle();

    await userEvent.click(screen.getByRole('button', { name: 'Trust 172.18.0.5' }));

    expect(screen.getByRole('textbox', { name: 'Proxy addresses' })).toHaveValue('172.18.0.5');
  });

  it('says why nothing can be saved when the configuration directory is read-only', async () => {
    serverAnswers({ settings: { ...serverSettings, writable: false } });

    renderWithProviders(ServerPage);
    await settle();

    expect(screen.getByText(/configuration directory isn't writable/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save and restart' })).toBeDisabled();
  });

  it('sends the edited values, and says when there was nothing to change', async () => {
    const fetched = serverAnswers({ saved: 204 });

    renderWithProviders(ServerPage);
    await settle();

    const days = screen.getByRole('textbox', { name: 'Days a session lasts' });
    await userEvent.clear(days);
    await userEvent.type(days, '45');
    await userEvent.click(screen.getByRole('button', { name: 'Save and restart' }));
    await settle();

    const put = fetched.mock.calls
      .map(([input]) => input as Request)
      .find((request) => request.method === 'PUT');
    const body = await put!.clone().json();

    expect(body.cookies.sessionDays).toBe(45);
    expect(await screen.findByText(/Nothing changed/)).toBeInTheDocument();
  });

  it('waits for the restart before saying the settings are in use', async () => {
    serverAnswers({ saved: 202 });

    renderWithProviders(ServerPage);
    await settle();

    await userEvent.click(screen.getByRole('button', { name: 'Save and restart' }));

    expect(
      await screen.findByText(
        'Saved. Culina is running with the new settings.',
        {},
        { timeout: 3000 }
      )
    ).toBeInTheDocument();
  });

  it('does not send a number it cannot read', async () => {
    const fetched = serverAnswers();

    renderWithProviders(ServerPage);
    await settle();

    const days = screen.getByRole('textbox', { name: 'Days a session lasts' });
    await userEvent.clear(days);
    await userEvent.type(days, 'a month');
    await userEvent.click(screen.getByRole('button', { name: 'Save and restart' }));
    await settle();

    expect(screen.getAllByText('Enter a whole number.').length).toBeGreaterThan(0);
    expect(fetched.mock.calls.some(([input]) => (input as Request).method === 'PUT')).toBe(false);
  });
});
