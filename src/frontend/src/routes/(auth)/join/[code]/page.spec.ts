import { screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import JoinPage from './+page.svelte';
import { session, type SessionStatus } from '$features/auth/session.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * One link, four people opening it: somebody signed out, somebody signed in
 * who is let in, the owner checking the link they are about to send, and
 * somebody holding a code that no longer works. Only the first is ever offered
 * a sign-in, and nobody signed in is left on a page with no way back.
 */
vi.mock('$app/state', () => ({ page: { params: { code: 'abc123' } } }));

const goto = vi.hoisted(() => vi.fn());

vi.mock('$app/navigation', () => ({ goto }));

const json = (body: unknown, status: number) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

/** Answers the redemption; every other request is the session re-reading itself. */
function redemptionAnswers(reply: () => Response) {
  const fetched = vi.fn((input: Request) =>
    Promise.resolve(input.url.includes('/redemptions') ? reply() : json({}, 200))
  );

  vi.stubGlobal('fetch', fetched);

  return fetched;
}

function signedIn(status: SessionStatus) {
  vi.spyOn(session, 'status', 'get').mockReturnValue(status);
  vi.spyOn(session, 'resolve').mockResolvedValue();
  vi.spyOn(session, 'reset').mockImplementation(() => {});
}

const signInOffered = () => screen.queryByRole('link', { name: /sign in/i });

beforeEach(() => goto.mockReset());

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('opening an invitation', () => {
  it('offers an account or a sign-in to somebody signed out, and comes back here after', async () => {
    signedIn('anonymous');
    const fetched = redemptionAnswers(() => json({}, 500));

    renderWithProviders(JoinPage);

    const signIn = await screen.findByRole('link', { name: /sign in/i });

    expect(signIn).toHaveAttribute(
      'href',
      expect.stringContaining(encodeURIComponent('/join/abc123'))
    );
    expect(fetched).not.toHaveBeenCalled();
  });

  it('lets somebody signed in straight into the household it is for', async () => {
    signedIn('authenticated');
    const select = vi.spyOn(session, 'selectHousehold').mockImplementation(() => {});

    redemptionAnswers(() =>
      json({ householdId: 'h2', name: 'Graces Küche', alreadyMember: false }, 201)
    );

    renderWithProviders(JoinPage);

    await vi.waitFor(() => expect(goto).toHaveBeenCalled());

    // The one the link was for, not whichever kitchen they had open last.
    expect(select).toHaveBeenCalledWith('h2');
  });

  it('tells the owner checking their own link that they are already in', async () => {
    signedIn('authenticated');
    redemptionAnswers(() => json({ householdId: 'h1', name: 'Home', alreadyMember: true }, 200));

    renderWithProviders(JoinPage);

    expect(
      await screen.findByRole('heading', { name: "You're already in Home" })
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Open Home' })).toBeVisible();
    expect(signInOffered()).not.toBeInTheDocument();
  });

  it('says a used, expired or revoked code is invalid, and offers the way back', async () => {
    signedIn('authenticated');
    redemptionAnswers(() =>
      json({ code: 'households.invitation_invalid', detail: 'That invitation is not valid.' }, 404)
    );

    renderWithProviders(JoinPage);

    expect(
      await screen.findByRole('heading', { name: 'That invitation is not valid' })
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to your kitchen' })).toHaveAttribute('href', '/');
    expect(signInOffered()).not.toBeInTheDocument();
  });

  it('does not call a failure to reach the server an invalid invitation', async () => {
    signedIn('authenticated');
    redemptionAnswers(() => json({ code: 'server.unexpected', detail: 'Oops.' }, 500));

    renderWithProviders(JoinPage);

    expect(await screen.findByRole('button', { name: 'Try again' })).toBeVisible();
    expect(screen.queryByText('That invitation is not valid')).not.toBeInTheDocument();
  });
});
