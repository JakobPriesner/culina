import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import SetupPage from './+page.svelte';
import { server } from '$features/server/stores/server.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * The steps a fresh instance walks its first visitor through, and the one
 * thing the browser knows that the server cannot: whether this page came
 * over HTTPS.
 */
const emptyDatabase = {
  host: '',
  port: 5432,
  name: '',
  username: '',
  passwordConfigured: false,
  requireSsl: true,
  maxPoolSize: 20,
  pinned: [],
  writable: true
};

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
  connection: { remoteAddress: '127.0.0.1', forwarded: false, proxyTrusted: false },
  pinned: [],
  writable: true
};

const json = (body: object, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

const unreachable = {
  type: 'urn:culina:problem:settings.database_unreachable',
  status: 400,
  detail:
    'Culina could not connect to that database: password authentication failed for user "culina_app".',
  code: 'settings.database_unreachable',
  requestId: 'r1'
};

/** A server at the database step; `connects` decides whether the database is accepted. */
function serverAnswers(connects: boolean) {
  let startedAt = '2026-09-24T10:00:00Z';
  let stage = 'database';

  const fetched = vi.fn((input: unknown) => {
    const request = input as Request;
    const url = request.url;

    if (request.method === 'PUT' && url.includes('/settings/database')) {
      if (!connects) return Promise.resolve(json(unreachable, 400));

      startedAt = '2026-09-24T10:00:02Z';
      stage = 'account';

      return Promise.resolve(new Response(null, { status: 202 }));
    }

    if (url.includes('/setup')) return Promise.resolve(json({ stage, startedAt }));
    if (url.includes('/settings/database')) return Promise.resolve(json(emptyDatabase));
    if (url.includes('/settings/server')) return Promise.resolve(json(serverSettings));

    return Promise.resolve(json({}, 404));
  });

  vi.stubGlobal('fetch', fetched);

  return fetched;
}

const settle = async () => {
  for (let turn = 0; turn < 5; turn += 1) {
    await new Promise((resume) => setTimeout(resume, 0));
  }
};

const atStage = (stage: string) => ({
  props: { data: { setup: { stage, startedAt: '2026-09-24T10:00:00Z' } } }
});

async function fillDatabase() {
  await userEvent.type(screen.getByRole('textbox', { name: 'Host' }), 'localhost');
  await userEvent.type(screen.getByRole('textbox', { name: 'Database name' }), 'culina');
  await userEvent.type(screen.getByRole('textbox', { name: 'User' }), 'culina_app');
  await userEvent.type(screen.getByLabelText('Password'), 'secret');
}

beforeEach(() => {
  server.clear();
});

describe('setting up a fresh instance', () => {
  it('starts with the database when there is none', async () => {
    serverAnswers(true);

    renderWithProviders(SetupPage, atStage('database'));
    await settle();

    expect(screen.getByText('Step 1 of 3')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Connect a database' })).toBeInTheDocument();
  });

  it('says why a database was refused, in the server’s own words too', async () => {
    serverAnswers(false);

    renderWithProviders(SetupPage, atStage('database'));
    await settle();
    await fillDatabase();
    await userEvent.click(screen.getByRole('button', { name: 'Connect' }));
    await settle();

    // The headline is translated; the reason is only the server's to give.
    expect(screen.getByText("Culina couldn't connect to that database.")).toBeInTheDocument();
    expect(screen.getByText(/password authentication failed/)).toBeInTheDocument();
  });

  it('goes on once the server is back with the database, cookies set for this page', async () => {
    serverAnswers(true);

    renderWithProviders(SetupPage, atStage('database'));
    await settle();
    await fillDatabase();
    await userEvent.click(screen.getByRole('button', { name: 'Connect' }));

    expect(
      await screen.findByRole('heading', { name: 'How people reach Culina' }, { timeout: 3000 })
    ).toBeInTheDocument();

    // The test page is plain HTTP, so secure cookies would lock this visitor
    // out: the step starts with them off, whatever the server's default.
    expect(await screen.findByRole('switch', { name: 'Secure cookies' })).toHaveAttribute(
      'aria-checked',
      'false'
    );
  });

  it('skips the database when the deployment already configured one', async () => {
    serverAnswers(true);

    renderWithProviders(SetupPage, atStage('account'));
    await settle();

    expect(screen.getByText('Step 1 of 2')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'How people reach Culina' })).toBeInTheDocument();
    expect(await screen.findByRole('textbox', { name: 'Proxy addresses' })).toBeInTheDocument();
  });
});
