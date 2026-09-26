import { fireEvent, screen, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { resetAllStores } from '$shell/stores';
import { renderWithProviders } from '$lib/test/render';

import InheritancePanel from './InheritancePanel.svelte';
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
      role: 'owner',
      inheritsFrom: [
        { householdId: 'h1', name: 'Home' },
        { householdId: 'p1', name: 'Grandma' }
      ]
    },
    { householdId: 'h3', name: 'Club', role: 'member', inheritsFrom: [] }
  ]
};

const settings = { locale: 'en', theme: 'warm-paper', mode: 'light', measurementSystem: 'metric' };

const json = (body: unknown) =>
  new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' } });

let send: ReturnType<typeof vi.fn>;

beforeEach(async () => {
  localStorage.clear();
  resetAllStores();
  send = vi.fn((input: Request) =>
    Promise.resolve(
      input.url.endsWith('/settings')
        ? json(settings)
        : input.url.endsWith('/inheritance')
          ? json({})
          : json(me)
    )
  );
  vi.stubGlobal('fetch', send);
  await session.refresh();
});

describe('inherited recipes', () => {
  it('says whose recipes a household sees, and through whom', () => {
    renderWithProviders(InheritancePanel, { props: { householdId: 'h2' } });

    expect(
      screen.getByText('Sees the recipes of Home. Through it, also those of Grandma.')
    ).toBeInTheDocument();
  });

  it('lets an owner choose another household, saved the moment it is chosen', async () => {
    renderWithProviders(InheritancePanel, { props: { householdId: 'h2' } });

    const field = screen.getByRole('combobox', { name: 'Inherits recipes from' });
    expect(field).toHaveValue('h1');

    await fireEvent.change(field, { target: { value: '' } });

    await waitFor(() => {
      const put = send.mock.calls
        .map(([request]) => request as Request)
        .find((request) => request.method === 'PUT');

      expect(put?.url).toMatch(/\/households\/h2\/inheritance$/);
    });

    const put = send.mock.calls
      .map(([request]) => request as Request)
      .find((r) => r.method === 'PUT');
    await expect(put!.json()).resolves.toEqual({ householdId: null });
  });

  it('offers only the other households, never the one itself', () => {
    renderWithProviders(InheritancePanel, { props: { householdId: 'h1' } });

    const options = screen
      .getAllByRole('option')
      .map((option) => (option as HTMLOptionElement).value);

    expect(options).toEqual(['', 'h2', 'h3']);
  });

  it('tells a member it is for an owner to change', () => {
    renderWithProviders(InheritancePanel, { props: { householdId: 'h3' } });

    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    expect(screen.getByText('Only an owner of this household can do that.')).toBeInTheDocument();
    expect(screen.getByText('Sees only its own recipes.')).toBeInTheDocument();
  });
});
