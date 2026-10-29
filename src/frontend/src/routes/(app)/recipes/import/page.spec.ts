import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import ImportPage from './+page.svelte';
import { session } from '$features/auth/session.svelte';
import { sources } from '$features/import/stores/sources.svelte';
import { renderWithProviders } from '#lib/test/render.js';

/*
 * Tests the page, not the store: the bug (a `list` that read the state it writes and re-triggered
 * its own `$effect` until a 429) only exists with a real effect running.
 */
const household = 'h1';

function serverAnswers(reply: () => Response) {
  const fetched = vi.fn(() => Promise.resolve(reply()));

  vi.stubGlobal('fetch', fetched);

  return fetched;
}

const noSources = () =>
  new Response(JSON.stringify({ items: [] }), {
    status: 200,
    headers: { 'Content-Type': 'application/json' }
  });

const settle = async () => {
  for (let turn = 0; turn < 5; turn += 1) {
    await new Promise((resume) => setTimeout(resume, 0));
  }
};

beforeEach(() => {
  sources.reset();

  vi.spyOn(session, 'activeHouseholdId', 'get').mockReturnValue(household);
});

describe('disconnecting an app', () => {
  it('says so when the server would not, rather than just putting the app back', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: Request) =>
        Promise.resolve(
          input.method === 'DELETE'
            ? new Response(JSON.stringify({ code: 'server.unavailable', detail: 'Nope' }), {
                status: 503,
                headers: { 'Content-Type': 'application/json' }
              })
            : new Response(
                JSON.stringify({
                  items: [
                    {
                      sourceId: 's1',
                      kind: 'tandoor',
                      label: 'recipes.example.com',
                      address: 'https://recipes.example.com',
                      createdAt: '2026-09-17T00:00:00Z',
                      lastUsedAt: null
                    }
                  ]
                }),
                { status: 200, headers: { 'Content-Type': 'application/json' } }
              )
        )
      )
    );

    renderWithProviders(ImportPage);
    await settle();

    await userEvent.click(screen.getByRole('button', { name: 'Disconnect' }));
    await settle();

    expect(screen.getByText('recipes.example.com')).toBeInTheDocument();
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });
});

describe('opening the import page', () => {
  it('asks for the connected apps exactly once', async () => {
    const fetched = serverAnswers(noSources);

    renderWithProviders(ImportPage);

    await settle();

    // One: every extra call is an effect that re-triggered itself.
    expect(fetched).toHaveBeenCalledTimes(1);
  });

  it('offers a way back when they cannot be read', async () => {
    serverAnswers(
      () =>
        new Response(JSON.stringify({ code: 'server.unavailable', detail: 'Nope' }), {
          status: 503,
          headers: { 'Content-Type': 'application/json' }
        })
    );

    renderWithProviders(ImportPage);

    await settle();

    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('does not keep retrying a failure by itself', async () => {
    const fetched = serverAnswers(
      () =>
        new Response(JSON.stringify({ code: 'server.unavailable', detail: 'Nope' }), {
          status: 503,
          headers: { 'Content-Type': 'application/json' }
        })
    );

    renderWithProviders(ImportPage);

    await settle();

    // Releasing the guard on failure must let the button ask again, never the effect, or a down
    // server is a request loop.
    expect(fetched).toHaveBeenCalledTimes(1);
  });
});
