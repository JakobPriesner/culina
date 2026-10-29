import { screen, waitFor, within } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '#lib/test/render.js';
import { resetAllStores } from '$shell/stores';
import { toaster } from '$shell/toaster.svelte';

import DevicesList from './DevicesList.svelte';
import { describeUserAgent } from './devices.svelte';

/* A lost phone or a leaked agent token is ended from here; the device reading the list is never one of the things it offers to end. */
const firefoxOnMac =
  'Mozilla/5.0 (Macintosh; Intel Mac OS X 14.6; rv:131.0) Gecko/20100101 Firefox/131.0';

const sessions = {
  items: [
    {
      sessionId: 'agent-1',
      client: 'agent',
      userAgent: null,
      ipAddress: null,
      createdAt: '2026-10-01T08:00:00Z',
      lastSeenAt: '2026-10-17T09:30:00Z',
      isCurrent: false
    },
    {
      sessionId: 'this-one',
      client: 'browser',
      userAgent: firefoxOnMac,
      ipAddress: '192.0.2.4',
      createdAt: '2026-10-01T08:00:00Z',
      lastSeenAt: '2026-10-18T10:00:00Z',
      isCurrent: true
    },
    {
      sessionId: 'phone',
      client: 'ios',
      userAgent: null,
      ipAddress: '198.51.100.7',
      createdAt: '2026-09-01T08:00:00Z',
      lastSeenAt: '2026-10-16T19:00:00Z',
      isCurrent: false
    }
  ]
};

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/problem+json' }
  });

let fetched: ReturnType<typeof vi.fn>;

function serve(revoke: () => Response = () => new Response(null, { status: 204 })) {
  fetched = vi.fn((input: Request) =>
    Promise.resolve(input.method === 'DELETE' ? revoke() : json(sessions))
  );
  vi.stubGlobal('fetch', fetched);
}

const deletes = () =>
  fetched.mock.calls.map(([request]) => request as Request).filter((r) => r.method === 'DELETE');

beforeEach(() => {
  resetAllStores();
  toaster.reset();
  serve();
});

describe('the list of devices', () => {
  it('says what each one is and when it was last used, this one first', async () => {
    renderWithProviders(DevicesList);

    await screen.findByText('AI agent');
    const rows = within(screen.getByRole('list', { name: 'Signed in' })).getAllByRole('listitem');

    expect(rows.map((row) => row.textContent)).toEqual([
      expect.stringContaining('Web browser'),
      expect.stringContaining('AI agent'),
      expect.stringContaining('iPhone or iPad app')
    ]);
    expect(rows[0]).toHaveTextContent('This device');
    expect(rows[0]).toHaveTextContent('Firefox · macOS');
    expect(rows[0]).toHaveTextContent('192.0.2.4');
    expect(rows[1]).toHaveTextContent(/Last used Oct 17, 2026/);
  });

  it('offers to sign out every device but this one', async () => {
    renderWithProviders(DevicesList);

    await screen.findByText('AI agent');
    const rows = screen.getAllByRole('listitem');

    expect(within(rows[0]!).queryByRole('button')).not.toBeInTheDocument();
    expect(within(rows[1]!).getByRole('button', { name: 'Sign out AI agent' })).toBeInTheDocument();
    expect(
      within(rows[2]!).getByRole('button', { name: 'Sign out iPhone or iPad app' })
    ).toBeInTheDocument();
  });

  it('says so when the list cannot be read, and tries again on request', async () => {
    fetched.mockImplementation(() =>
      Promise.resolve(json({ code: 'server.unavailable', title: 'Unavailable', status: 503 }, 503))
    );

    renderWithProviders(DevicesList);

    expect(await screen.findByText("Your devices couldn't be loaded.")).toBeInTheDocument();

    serve();
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('AI agent')).toBeInTheDocument();
  });
});

describe('signing a device out', () => {
  it('ends that session and takes it off the list at once', async () => {
    renderWithProviders(DevicesList);

    await userEvent.click(await screen.findByRole('button', { name: 'Sign out AI agent' }));

    expect(deletes()).toHaveLength(1);
    expect(deletes()[0]!.url).toMatch(/\/api\/v1\/sessions\/agent-1$/);
    expect(screen.queryByText('AI agent')).not.toBeInTheDocument();
    await waitFor(() => expect(toaster.toasts.at(-1)?.message()).toBe('Signed out.'));
  });

  it('puts it back when the server refuses', async () => {
    serve(() => json({ code: 'sessions.not_found', title: 'Not found', status: 404 }, 404));

    renderWithProviders(DevicesList);

    await userEvent.click(await screen.findByRole('button', { name: 'Sign out AI agent' }));

    expect(await screen.findByText('AI agent')).toBeInTheDocument();
    await waitFor(() => expect(toaster.toasts.at(-1)?.tone).toBe('danger'));
  });
});

describe('naming a browser', () => {
  it.each([
    [firefoxOnMac, 'Firefox · macOS'],
    [
      'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0 Safari/537.36 Edg/129.0',
      'Edge · Windows'
    ],
    [
      'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1',
      'Safari · iOS'
    ],
    ['curl/8.7.1', null],
    [null, null]
  ])('reads %s as %s', (userAgent, name) => {
    expect(describeUserAgent(userAgent)).toBe(name);
  });
});
