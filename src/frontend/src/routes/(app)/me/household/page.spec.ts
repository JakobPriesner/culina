import { screen, waitFor, within } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import HouseholdPage from './+page.svelte';
import { session } from '$features/auth/session.svelte';
import { resetAllStores } from '$shell/stores';
import { toaster } from '$shell/toaster.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * Deleting a household, from the page that offers it. The server decides who
 * may — these prove the page does not offer it to anybody else, sends the
 * version it just read rather than one it guessed, and says so when the
 * server refuses anyway.
 */
const goto = vi.hoisted(() => vi.fn());

vi.mock('$app/navigation', () => ({ goto, invalidateAll: vi.fn() }));

const home = (role: 'owner' | 'member') => ({
  householdId: 'h1',
  name: 'Home',
  role,
  inheritsFrom: []
});
const flat = { householdId: 'h2', name: 'Flat', role: 'owner', inheritsFrom: [] };

const membership = (households: object[]) => ({
  userId: 'u1',
  email: 'ada@example.com',
  displayName: 'Ada',
  isAdmin: false,
  createdAt: '2026-01-01T00:00:00Z',
  version: 1,
  assistance: { improve: false, draft: false, read: false, draw: false },
  households
});

const settings = { locale: 'en', theme: 'warm-paper', mode: 'light', measurementSystem: 'metric' };

const json = (body: unknown, status = 200) =>
  new Response(status === 204 ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });

let fetched: ReturnType<typeof vi.fn>;

/** Every request the page makes, answered; the delete with whatever the test says. */
function serve(
  role: 'owner' | 'member',
  deleteAnswer: () => Response = () => json(null, 204),
  others: object[] = []
) {
  let deleted = false;

  fetched = vi.fn((input: Request) => {
    const url = new URL(input.url);

    if (input.method === 'DELETE') {
      const answer = deleteAnswer();
      deleted = answer.ok;

      return Promise.resolve(answer);
    }

    if (input.method === 'POST' && url.pathname.endsWith('/restorations')) {
      return Promise.resolve(json(null, 204));
    }

    if (url.pathname === '/api/v1/users/me') {
      return Promise.resolve(json(membership(deleted ? others : [home(role), ...others])));
    }

    if (url.pathname.endsWith('/settings')) {
      return Promise.resolve(json(settings));
    }

    if (url.pathname === '/api/v1/households/h1') {
      return Promise.resolve(
        json({
          householdId: 'h1',
          name: 'Home',
          createdAt: '2026-01-01T00:00:00Z',
          members: [],
          version: 7
        })
      );
    }

    return Promise.resolve(json({ items: [] }));
  });

  vi.stubGlobal('fetch', fetched);
}

const sent = (method: string) =>
  fetched.mock.calls.map(([request]) => request as Request).filter((r) => r.method === method);

const deletes = () => sent('DELETE');

beforeEach(() => {
  localStorage.clear();
  resetAllStores();
  toaster.reset();
  goto.mockReset();

  // jsdom has <dialog> but not the top layer, so showModal is the open state.
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };

  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.open = false;
    this.dispatchEvent(new Event('close'));
  };
});

describe('deleting a household', () => {
  it('is not offered to a member who is not an owner', async () => {
    serve('member');
    await session.refresh();

    renderWithProviders(HouseholdPage);

    expect(screen.queryByRole('button', { name: 'Delete household' })).not.toBeInTheDocument();
  });

  it('asks first, then deletes with the version it has just read', async () => {
    serve('owner');
    await session.refresh();
    renderWithProviders(HouseholdPage);

    await userEvent.click(screen.getByRole('button', { name: 'Delete household' }));

    const dialog = screen.getByRole('dialog', { name: 'Delete Home?' });
    expect(within(dialog).getByText(/Everyone in it loses access at once/)).toBeInTheDocument();
    expect(deletes()).toHaveLength(0);

    await userEvent.click(within(dialog).getByRole('button', { name: 'Delete household' }));

    await waitFor(() => expect(deletes()).toHaveLength(1));
    expect(deletes()[0]!.url).toMatch(/\/api\/v1\/households\/h1$/);
    expect(deletes()[0]!.headers.get('If-Match')).toBe('"v7"');
    await waitFor(() => expect(goto).toHaveBeenCalledWith('/', { replaceState: true }));
  });

  it('names, and undoes, the household that was deleted — not the one shown next', async () => {
    serve('owner', undefined, [flat]);
    await session.refresh();
    session.selectHousehold('h1');
    renderWithProviders(HouseholdPage);

    await userEvent.click(screen.getByRole('button', { name: 'Delete household' }));
    const dialog = screen.getByRole('dialog', { name: 'Delete Home?' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Delete household' }));

    // Once the session is read again the page is about Flat; the message and
    // its Undo must still be about Home.
    await waitFor(() => expect(toaster.toasts).toHaveLength(1));
    const toast = toaster.toasts[0]!;
    expect(toast.message()).toBe('Home was deleted.');

    toaster.act(toast.id);

    await waitFor(() => expect(sent('POST')).toHaveLength(1));
    expect(sent('POST')[0]!.url).toMatch(/\/api\/v1\/households\/h1\/restorations$/);
  });

  it('says so when the server refuses, and stays where it is', async () => {
    serve('owner', () =>
      json(
        {
          type: 'about:blank',
          title: 'Forbidden',
          status: 403,
          code: 'households.not_owner',
          detail: 'Only an owner of this household can do that.'
        },
        403
      )
    );
    await session.refresh();
    renderWithProviders(HouseholdPage);

    await userEvent.click(screen.getByRole('button', { name: 'Delete household' }));
    const dialog = screen.getByRole('dialog', { name: 'Delete Home?' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Delete household' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      "The household couldn't be deleted."
    );
    expect(goto).not.toHaveBeenCalled();
  });
});
