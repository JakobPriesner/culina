import { fireEvent, screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { session } from '$features/auth/session.svelte';
import { resetAllStores } from '$shell/stores';
import { update } from '$shell/updates.svelte';
import { renderWithProviders } from '$lib/test/render';

import AccountMenu from './AccountMenu.svelte';

const goto = vi.hoisted(() => vi.fn());
const page = vi.hoisted(() => ({ url: new URL('http://culina.test/') }));

vi.mock('$app/navigation', () => ({ goto }));
vi.mock('$app/state', () => ({ page }));

const me = {
  userId: 'u1',
  email: 'ada@example.com',
  displayName: 'Ada Lovelace',
  isAdmin: false,
  createdAt: '2026-01-01T00:00:00Z',
  version: 1,
  assistance: { improve: false, draft: false, read: false, draw: false },
  households: [{ householdId: 'h1', name: 'Home', role: 'owner', inheritsFrom: [] }]
};

const settings = { locale: 'en', theme: 'warm-paper', mode: 'light', measurementSystem: 'metric' };

const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

// jsdom implements neither `showPopover` nor the declarative invocation, so the panel stays hidden
// here; its contents are still there to be pressed. The end-to-end suite opens it for real.
const hidden = { hidden: true } as const;

const fetch = vi.fn((input: Request) =>
  Promise.resolve(input.url.endsWith('/settings') ? json(settings) : json(me))
);

beforeEach(async () => {
  localStorage.clear();
  resetAllStores();
  goto.mockClear();
  fetch.mockClear();
  page.url = new URL('http://culina.test/');
  vi.stubGlobal('fetch', fetch);
  await session.refresh();
  session.selectHousehold('h1');
});

afterEach(() => {
  update.reset();
});

describe('the account menu', () => {
  it('opens from one button that says what it holds', () => {
    renderWithProviders(AccountMenu, { props: { onswitch: vi.fn() } });

    expect(screen.getByRole('button', { name: 'Your account and settings' })).toBeInTheDocument();
  });

  it('says who is signed in', () => {
    renderWithProviders(AccountMenu, { props: { onswitch: vi.fn() } });

    expect(screen.getByText('Ada Lovelace')).toBeInTheDocument();
    expect(screen.getByText('ada@example.com')).toBeInTheDocument();
  });

  it('holds the household, theme and language', () => {
    renderWithProviders(AccountMenu, { props: { onswitch: vi.fn() } });

    expect(screen.getByRole('button', { name: 'Home', ...hidden })).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'Theme', ...hidden })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Dark', ...hidden })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Language', ...hidden })).toBeInTheDocument();
  });

  it('leads to settings', () => {
    renderWithProviders(AccountMenu, { props: { onswitch: vi.fn() } });

    expect(screen.getByRole('link', { name: 'Settings', ...hidden })).toHaveAttribute(
      'href',
      '/me'
    );
  });

  it('opens settings on ⌘, or Ctrl-,', async () => {
    renderWithProviders(AccountMenu, { props: { onswitch: vi.fn() } });

    await fireEvent.keyDown(window, { key: ',', metaKey: true });
    await fireEvent.keyDown(window, { key: ',', ctrlKey: true });
    await fireEvent.keyDown(window, { key: ',' });

    expect(goto).toHaveBeenCalledTimes(2);
    expect(goto).toHaveBeenCalledWith('/me');
  });

  it('signs out and lands on sign-in', async () => {
    renderWithProviders(AccountMenu, { props: { onswitch: vi.fn() } });

    await fireEvent.click(screen.getByRole('button', { name: 'Sign out', ...hidden }));

    await vi.waitFor(() => expect(goto).toHaveBeenCalledWith('/login', { replaceState: true }));
    expect(fetch.mock.calls.some(([request]) => request.method === 'DELETE')).toBe(true);
  });

  it('says on its button when an update is waiting', () => {
    update.hold({} as ServiceWorker);
    renderWithProviders(AccountMenu, { props: { onswitch: vi.fn() } });

    expect(
      screen.getByRole('button', { name: 'Your account and settings, an update is ready' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /A new version of Culina is ready/, ...hidden })
    ).toBeInTheDocument();
  });
});
