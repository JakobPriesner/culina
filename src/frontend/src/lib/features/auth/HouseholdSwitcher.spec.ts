import { fireEvent, screen } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { resetAllStores } from '$shell/stores';
import { renderWithProviders } from '$lib/test/render';

import HouseholdSwitcher from './HouseholdSwitcher.svelte';
import { session } from './session.svelte';

const me = {
  userId: 'u1',
  email: 'jakob@example.com',
  displayName: 'Jakob',
  isAdmin: false,
  createdAt: '2026-01-01T00:00:00Z',
  version: 1,
  assistance: { improve: false, draft: false, read: false, draw: false },
  households: [
    { householdId: 'h1', name: 'Home', role: 'owner', inheritsFrom: [] },
    {
      householdId: 'h2',
      name: 'Flat',
      role: 'member',
      inheritsFrom: [{ householdId: 'h1', name: 'Home' }]
    }
  ]
};

const settings = { locale: 'en', theme: 'warm-paper', mode: 'light', measurementSystem: 'metric' };

const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

// jsdom implements neither `showPopover` nor the declarative invocation, so
// the panel stays hidden here; its contents are still there to be pressed. The
// end-to-end suite is where a real browser opens it.
const hidden = { hidden: true } as const;

beforeEach(async () => {
  localStorage.clear();
  resetAllStores();
  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request) =>
      Promise.resolve(input.url.endsWith('/settings') ? json(settings) : json(me))
    )
  );
  await session.refresh();
  session.selectHousehold('h1');
});

describe('the household switcher', () => {
  it('names the household on screen', () => {
    renderWithProviders(HouseholdSwitcher, { props: { onswitch: vi.fn() } });

    expect(screen.getByRole('button', { name: 'Home, switch household' })).toBeInTheDocument();
  });

  it('switches to another household, and says so', async () => {
    const onswitch = vi.fn();
    renderWithProviders(HouseholdSwitcher, { props: { onswitch } });

    await fireEvent.click(screen.getByRole('button', { name: /Flat/, ...hidden }));

    expect(session.activeHouseholdId).toBe('h2');
    expect(onswitch).toHaveBeenCalledOnce();
  });

  it('does not count choosing the household already on screen as a switch', async () => {
    const onswitch = vi.fn();
    renderWithProviders(HouseholdSwitcher, { props: { onswitch } });

    await fireEvent.click(screen.getByRole('button', { name: 'Home', ...hidden }));

    expect(onswitch).not.toHaveBeenCalled();
  });

  it('says whose recipes a household inherits', () => {
    renderWithProviders(HouseholdSwitcher, { props: { onswitch: vi.fn() } });

    expect(screen.getByText('Sees the recipes of Home.')).toBeInTheDocument();
  });

  it('offers a new household', () => {
    renderWithProviders(HouseholdSwitcher, { props: { onswitch: vi.fn() } });

    expect(screen.getByRole('button', { name: 'New household', ...hidden })).toBeInTheDocument();
  });
});
