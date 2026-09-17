import { screen } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import ImportPage from './+page.svelte';
import { session } from '$features/auth/session.svelte';
import { sources } from '$features/import/stores/sources.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * The page is tested rather than the store, because the bug this is here to
 * stop only exists once a real `$effect` is running: a `list` that read the
 * state it writes re-triggered the effect that called it, and the page asked
 * for the same thing until the server started answering 429.
 *
 * A store test cannot see that. Nothing about `list` in isolation is wrong —
 * it is the pairing with the effect that is, so the effect has to be real.
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

/** Lets every queued effect and the request it made settle. */
const settle = async () => {
  for (let turn = 0; turn < 5; turn += 1) {
    await new Promise((resume) => setTimeout(resume, 0));
  }
};

beforeEach(() => {
  sources.reset();

  // The page reads the active household from the session and nothing else.
  vi.spyOn(session, 'activeHouseholdId', 'get').mockReturnValue(household);
});

describe('opening the import page', () => {
  it('asks for the connected apps exactly once', async () => {
    const fetched = serverAnswers(noSources);

    renderWithProviders(ImportPage);

    await settle();

    // One. Not "a reasonable number": every extra call here is an effect that
    // re-triggered itself, and the next one after that is the rate limiter.
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

    // Without this the page is a heading and a footer link: no list, no connect
    // form, and nothing to press.
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

    // Releasing the guard on failure must let the *button* ask again, never
    // the effect — otherwise a server that is down is a request loop.
    expect(fetched).toHaveBeenCalledTimes(1);
  });
});
