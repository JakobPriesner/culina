import { screen } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import SettingsLayout from './+layout.svelte';
import { session } from '$features/auth/session.svelte';
import { olliSetting } from '$shell/olli/setting.svelte';
import { resetAllStores } from '$shell/stores';
import { renderWithProviders } from '$lib/test/render';

/*
 * The two settings pages only an administrator may open, opened by somebody
 * else: by a typed address or a link, since the rail never offers them.
 */
const location = vi.hoisted(() => ({ pathname: '/me/server' }));

vi.mock('$app/state', () => ({
  page: {
    get url() {
      return new URL(`http://culina.test${location.pathname}`);
    }
  }
}));

function signedInAs(isAdmin: boolean) {
  vi.stubGlobal(
    'fetch',
    vi.fn(() =>
      Promise.resolve(
        new Response(
          JSON.stringify({
            userId: 'u1',
            email: 'ada@example.com',
            displayName: 'Ada',
            isAdmin,
            createdAt: '2026-01-01T00:00:00Z',
            version: 1,
            assistance: { improve: false, draft: false, read: false, draw: false },
            households: [{ householdId: 'h1', name: 'Home', role: 'owner', inheritsFrom: [] }]
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        )
      )
    )
  );

  return session.refresh();
}

const children = createRawSnippet(() => ({ render: () => '<p>The server settings</p>' }));

beforeEach(() => {
  resetAllStores();
  olliSetting.reset();
  location.pathname = '/me/server';
});

describe('an administrator-only settings page', () => {
  it.each(['/me/server', '/me/ai'])(
    'tells a member that %s is not theirs to change',
    async (path) => {
      location.pathname = path;
      await signedInAs(false);

      renderWithProviders(SettingsLayout, { props: { children } });

      expect(
        screen.getByRole('heading', { level: 1, name: 'Only the head chef touches this stove' })
      ).toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'Back to settings' })).toHaveAttribute('href', '/me');
      // The page itself never renders, so it never asks the server for settings it would refuse.
      expect(screen.queryByText('The server settings')).not.toBeInTheDocument();
    }
  );

  it('puts the refused order on a ticket when Olli is turned off', async () => {
    olliSetting.show(false);
    location.pathname = '/me/ai';
    await signedInAs(false);

    renderWithProviders(SettingsLayout, { props: { children } });

    // The order on the ticket is the page that was refused.
    expect(screen.getByText('1 × Assistant')).toBeInTheDocument();
  });

  it('opens as usual for an administrator', async () => {
    await signedInAs(true);

    renderWithProviders(SettingsLayout, { props: { children } });

    expect(screen.getByText('The server settings')).toBeInTheDocument();
    expect(screen.queryByText('Only the head chef touches this stove')).not.toBeInTheDocument();
  });

  it('leaves every other settings page alone', async () => {
    location.pathname = '/me/appearance';
    await signedInAs(false);

    renderWithProviders(SettingsLayout, { props: { children } });

    expect(screen.getByText('The server settings')).toBeInTheDocument();
  });
});
